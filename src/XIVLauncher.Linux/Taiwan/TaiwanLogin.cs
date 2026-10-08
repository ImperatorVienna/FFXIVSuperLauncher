using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace XIVLauncher.Linux.Taiwan;

public sealed class TaiwanLogin(HttpClient client)
{
    public static string EncodeCredential(string value) => string.Concat(value.Select(c => ((int)c).ToString("x2")));
    public const string LoginUrl = "https://user.ffxiv.com.tw/api/login/launcherLogin";
    public const string SessionUrl = "https://user.ffxiv.com.tw/api/login/launcherSession";
    public async Task<string> LoginAsync(string gamePath, string email, string password, string otp, string captcha, CancellationToken token, Func<CancellationToken, Task>? updateAfterLogin = null)
    {
        var loginToken = await AuthenticateAsync(email, password, otp, captcha, token);
        if (updateAfterLogin != null) await updateAfterLogin(token);
        return await CompleteAsync(gamePath, loginToken, token);
    }
    public async Task<string> AuthenticateAsync(string email, string password, string otp, string captcha, CancellationToken token)
    {
        using var request = CreateRequest(LoginUrl, JsonSerializer.Serialize(new
        { email = EncodeCredential(email), password = EncodeCredential(password), code = otp, token = captcha }));
        using var response = await client.SendAsync(request, token);
        using var login = await ReadResponse(response, "launcherLogin", token);
        if (!login.RootElement.TryGetProperty("token", out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw Rejected("launcherLogin", login.RootElement, email, password, otp, captcha).WithStatus((int)response.StatusCode);
        return value.GetString()!;
    }
    public async Task<string> CompleteAsync(string gamePath, string loginToken, CancellationToken token)
    {
        // The official launcher's OnStartGame requests launcherSession directly.
        // Game version checks belong to the shared update stage, not authentication.
        // Repeating xl_tw's distinct text/plain handshake here can return HTTP 404
        // even after a successful patch check and valid account authentication.
        using var sessionRequest = CreateRequest(SessionUrl, JsonSerializer.Serialize(new { token = loginToken }));
        using var sessionResponse = await client.SendAsync(sessionRequest, token);
        using var session = await ReadResponse(sessionResponse, "launcherSession", token);
        if (!session.RootElement.TryGetProperty("sessionId", out var sid) || sid.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(sid.GetString()))
            throw Rejected("launcherSession", session.RootElement, loginToken).WithStatus((int)sessionResponse.StatusCode);
        var result = sid.GetString()!;
        if (result.Any(c => char.IsWhiteSpace(c) || c is '"' or '\\')) throw new IOException("台服会话格式异常。");
        return result;
    }
    internal static HttpRequestMessage CreateRequest(string url, string body, string mediaType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        // Match xl_tw LoginBridge, including a fixed Content-Length (PostAsJsonAsync streams chunked JSON).
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Orbit/1.0)");
        request.Headers.AcceptLanguage.ParseAdd("en-US, en"); request.Headers.Accept.ParseAdd("*/*");
        return request;
    }
    internal static TaiwanAuthenticationException Rejected(string stage, JsonElement data, params string[] secrets)
    {
        string error = "";
        // Display only the dedicated human-readable error field, never arbitrary response JSON.
        foreach (var key in new[] { "error", "message", "msg" })
            if (data.TryGetProperty(key, out var field))
            {
                if (field.ValueKind == JsonValueKind.String) error = field.GetString() ?? "";
                else if (field.ValueKind == JsonValueKind.Object && field.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String) error = message.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(error)) break;
            }
        var reason = error.Contains("captcha", StringComparison.OrdinalIgnoreCase) || error.Contains("機器人") || error.Contains("机器人") ? "captcha"
            : error.Contains("OTP", StringComparison.OrdinalIgnoreCase) || error.Contains("2FA", StringComparison.OrdinalIgnoreCase) ? "otp"
            : error.Contains("連線") || error.Contains("连接") ? "server-connection"
            : error.Contains("密碼") || error.Contains("密码") || error.Contains("帳號") || error.Contains("账号") ? "credentials"
            : "rejected";
        foreach (var secret in secrets.Where(s => !string.IsNullOrEmpty(s)).SelectMany(s => new[] { s, EncodeCredential(s) }).OrderByDescending(s => s.Length))
            error = error.Replace(secret, "[redacted]", StringComparison.OrdinalIgnoreCase);
        error = System.Text.RegularExpressions.Regex.Replace(error, @"https?://\S+|[\w.+-]+@[\w.-]+|[A-Za-z0-9_+/=-]{24,}", "[redacted]");
        error = new string(error.Where(c => !char.IsControl(c)).ToArray());
        if (error.Length > 240) error = error[..240] + "…";
        return new(stage, null, reason, string.IsNullOrWhiteSpace(error) ? "服务端未提供可显示的错误说明。" : error);
    }
    private static async Task<JsonDocument> ReadResponse(HttpResponseMessage response, string stage, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode) throw new TaiwanAuthenticationException(stage, (int)response.StatusCode, "http");
        try
        {
            var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose(); throw new TaiwanAuthenticationException(stage, null, "response-format");
        }
        catch (JsonException) { throw new TaiwanAuthenticationException(stage, null, "response-format"); }
    }
    public static string GameArguments(string sessionId) => "DEV.LobbyHost01=neolobby01.ffxiv.com.tw DEV.LobbyPort01=54994 DEV.GMServerHost=frontier.ffxiv.com.tw DEV.TestSID=" + sessionId + " SYS.resetConfig=0 DEV.SaveDataBankHost=config-dl.ffxiv.com.tw";
}

public sealed class TaiwanAuthenticationException(string stage, int? httpStatus, string reason, string? serverMessage = null) : IOException(
    $"台服验证失败（{stage} / {reason}{(httpStatus is null ? "" : " / HTTP " + httpStatus)}）。" +
    (reason == "server-connection" ? "登录服务返回连接错误；此提示无法确定是网页验证、验证码还是服务端异常。" : reason == "captcha" ? "网页验证被拒绝，请重新验证。" : reason == "otp" ? "请检查 2FA 验证码与系统时间。" :
     stage == "launcherSession" ? "未能取得游戏会话；这不一定表示订阅无效。" : "请检查账号、密码与网页验证后重试。") + (serverMessage == null ? "" : " 服务端提示：" + serverMessage))
{
    public int? HttpStatus { get; private set; } = httpStatus;
    public TaiwanAuthenticationException WithStatus(int status) { HttpStatus = status; return this; }
    public string Diagnostic => $"Taiwan authentication failed: stage={stage}; reason={reason}; HTTP={HttpStatus?.ToString() ?? "n/a"}.";
}

