using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using XIVLauncher.Common.Encryption;
using XIVLauncher.Common.Util;
using XIVLauncher.Login.Models;
namespace XIVLauncher.Linux.Global;

public sealed record GlobalSession(string Id, int Region, int Expansion);

// Wire fields and callback indices follow xl/Common/Game/Launcher.cs; no response bodies or tokens in logs.
public sealed class GlobalLogin(HttpClient client) : IRegionLogin
{
    public Task<LoginArea[]> AreasAsync(CancellationToken token) => Task.FromResult(Array.Empty<LoginArea>());
    public async Task<string> AuthenticateAsync(RegionLoginContext context, CancellationToken token)
    {
        var settings = context.Settings;
        if (string.IsNullOrWhiteSpace(settings.Account) || string.IsNullOrEmpty(context.Password))
            throw new CredentialValidationException("ffxiv account and password are required.", "请填写 Square Enix 账号和密码，或读取已保存的密码。");
        using var steam = new SteamAuthentication();
        var ticket = settings.Current.SteamAccount ? await steam.GetAsync(settings.Current.FreeTrial, token) : null;
        var frontier = await FrontierAsync(token);
        var session = await LoginAsync(settings.Account, context.Password, settings.Current.FreeTrial, ticket,
            GameClientLanguage.For(settings), async ct =>
            {
                var secret = await LoginCode.SavedSecretForLoginAsync(settings, ct);
                return await LoginCode.GetAsync(secret, context.RequestManualOtp ?? (_ => Task.FromResult(context.ManualOtp)), ct);
            }, token, frontier);
        if (context.UpdateAfterLogin != null) await context.UpdateAfterLogin(token);
        var updater = new GlobalGameUpdater(client);
        var uid = await updater.RegisterAndUpdateAsync(settings.GamePath, settings.RegionRoot, session, context.Status, token);
        token.ThrowIfCancellationRequested();
        RegionUpdateService.RecordGameVersion(settings);
        return Arguments(uid, session, settings.GamePath, GameClientLanguage.For(settings), settings.Current.SteamAccount);
    }
    public async Task<GlobalSession> LoginAsync(string account, string password, bool trial, Ticket? ticket, int language,
        Func<CancellationToken, Task<string>> otp, CancellationToken token, string? frontier = null)
    {
        var top = "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=" + (trial ? "1" : "0") + "&cssmode=1&isnew=1&launchver=3";
        if (ticket != null) top += "&issteam=1&session_ticket=" + Uri.EscapeDataString(ticket.Text) + "&ticket_size=" + ticket.Length;
        using var request = Request(HttpMethod.Get, top, string.Format(frontier ?? DefaultFrontier, new[] { "ja", "en_us", "de", "fr" }[Math.Clamp(language, 0, 3)], DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm")));
        using var response = await client.SendAsync(request, token); response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token);
        if (html.Contains("window.external.user(\"restartup\");")) throw new IOException("ffxiv Steam account linking is required. Link accounts using the official launcher first.");
        var stored = Hidden(html, "_STORED_");
        if (ticket != null)
        {
            var linked = Hidden(html, "sqexid");
            if (!string.Equals(account, linked, StringComparison.OrdinalIgnoreCase))
                throw new CredentialValidationException("ffxiv Steam is linked to a different Square Enix account.", "当前 Steam 绑定的 Square Enix 账号与输入账号不一致，请切换 Steam 账号或修改登录账号。");
            account = linked;
        }
        var code = await otp(token);
        if (code.Length != 0 && !OneTimePassword.IsManualCode(code)) throw new IOException("ffxiv OTP must contain six digits.");
        using var login = Request(HttpMethod.Post, "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send", top);
        login.Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["_STORED_"] = stored, ["sqexid"] = account, ["password"] = password, ["otppw"] = code });
        using var result = await client.SendAsync(login, token); result.EnsureSuccessStatusCode();
        return ParseSession(await result.Content.ReadAsStringAsync(token), ticket != null, (int)result.StatusCode, account, password, code, stored, ticket?.Text ?? "");
    }
    internal static string Hidden(string html, string name)
    {
        foreach (Match tag in Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase))
        {
            string Attribute(string key) { var match = Regex.Match(tag.Value, "\\b" + key + "\\s*=\\s*([\"'])(.*?)\\1", RegexOptions.IgnoreCase); return WebUtility.HtmlDecode(match.Groups[2].Value); }
            if (Attribute("name") == name && Attribute("value") is { Length: > 0 } value) return value;
        }
        throw new IOException("ffxiv authentication response is missing a required field.");
    }
    internal static GlobalSession ParseSession(string html, bool steam = false, int? httpStatus = null, params string[] sensitiveValues)
    {
        // Read only the launcher's callback argument, never arbitrary page text.
        // Decode JS/JSON string escapes before splitting protocol fields.
        var callbacks = Regex.Matches(html, """window\s*\.\s*external\s*\.\s*user\s*\(\s*("(?:\\.|[^"\\])*")\s*\)""");
        foreach (Match callback in callbacks)
        {
            string? value;
            try { value = System.Text.Json.JsonSerializer.Deserialize<string>(callback.Groups[1].Value); }
            catch (System.Text.Json.JsonException) { continue; }
            if (value == null) continue;
            const string success = "login=auth,ok,";
            const string rejected = "login=auth,ng,err,";
            if (value.StartsWith(rejected, StringComparison.Ordinal))
                throw new GlobalAuthenticationException("server-rejected", steam, httpStatus,
                    SanitizeServerMessage(value[rejected.Length..], sensitiveValues));
            if (!value.StartsWith(success, StringComparison.Ordinal)) continue;
            var values = value[success.Length..].Split(',');
            if (values.Length < 14 || !int.TryParse(values[5], out var region) || !int.TryParse(values[13], out var expansion) || expansion is < 0 or > 5 || string.IsNullOrWhiteSpace(values[1]))
                throw new GlobalAuthenticationException("invalid-session", steam, httpStatus);
            if (values[3] == "0") throw new IOException("ffxiv terms must be accepted in the official launcher.");
            if (values[9] == "0") throw new IOException("ffxiv account has no playable service subscription.");
            return new(values[1], region, expansion);
        }
        throw new GlobalAuthenticationException("unexpected-response", steam, httpStatus);
    }
    private static string SanitizeServerMessage(string message, IEnumerable<string> secrets)
    {
        message = WebUtility.HtmlDecode(message);
        foreach (var secret in secrets.Where(x => !string.IsNullOrEmpty(x)).SelectMany(x => new[] { x, Uri.EscapeDataString(x), WebUtility.HtmlEncode(x) }).OrderByDescending(x => x.Length))
            message = message.Replace(secret, "[redacted]", StringComparison.OrdinalIgnoreCase);
        message = Regex.Replace(message, "<[^>]*>", " ");
        message = Regex.Replace(message, @"https?://\S+|[\w.+-]+@[\w.-]+|[A-Za-z0-9_+/=-]{24,}|\b\d{6}\b", "[redacted]");
        message = Regex.Replace(message, @"[\p{Cc}\p{Cf}]+|\s+", " ").Trim();
        return message.Length > 320 ? message[..320] + "…" : message;
    }
    private const string DefaultFrontier = "https://launcher.finalfantasyxiv.com/v740/index.html?rc_lang={0}&time={1}";
    private async Task<string> FrontierAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("https://kamori.goats.dev/Launcher/GetLauncherClientConfig", timeout.Token));
            var template = json.RootElement.GetProperty("frontierUrl").GetString()!;
            var uri = new Uri(string.Format(template, "en_us", "0"));
            if (uri.Scheme == "https" && uri.Host == "launcher.finalfantasyxiv.com") return template;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or KeyNotFoundException or FormatException)
        { token.ThrowIfCancellationRequested(); }
        return DefaultFrontier;
    }
    private static string ComputerId()
    {
        var hash = System.Security.Cryptography.SHA1.HashData(Encoding.Unicode.GetBytes(Environment.MachineName + Environment.UserName + Environment.OSVersion + Environment.ProcessorCount));
        var bytes = new byte[5]; Array.Copy(hash, 0, bytes, 1, 4);
        bytes[0] = unchecked((byte)-(bytes[1] + bytes[2] + bytes[3] + bytes[4]));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    private static HttpRequestMessage Request(HttpMethod method, string url, string referer)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.TryAddWithoutValidation("User-Agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; " + ComputerId() + ")");
        message.Headers.TryAddWithoutValidation("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
        message.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9"); message.Headers.TryAddWithoutValidation("Cookie", "_rsid=\"\"");
        message.Headers.Referrer = new Uri(referer); return message;
    }
    public static string Arguments(string uid, GlobalSession session, string game, int language, bool steam)
    {
        var args = new ArgumentBuilder().Append("DEV.DataPathType", "1").Append("DEV.MaxEntitledExpansionID", session.Expansion.ToString())
            .Append("DEV.TestSID", uid).Append("DEV.UseSqPack", "1").Append("SYS.Region", session.Region.ToString())
            .Append("language", language.ToString()).Append("resetConfig", "0").Append("ver", Patching.GamePatchFiles.Version(game));
        if (steam) args.Append("IsSteam", "1");
        return args.Build();
    }
}
