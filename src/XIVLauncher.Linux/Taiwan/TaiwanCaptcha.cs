using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace XIVLauncher.Linux.Taiwan;

/// <summary>Linux browser equivalent of xl_tw's WebView2 page. The browser sees only CAPTCHA, never account credentials.</summary>
public static class TaiwanCaptcha
{
    public const string PageUrl = "https://launcher.ffxiv.com.tw/index.html";
    public const string SiteKey = "6Ld6VmorAAAAANQdQeqkaOeScR42qHC7Hyalq00r";
    internal static FileStream OpenProfile(string profile)
    {
        Directory.CreateDirectory(profile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (new DirectoryInfo(profile).LinkTarget != null) throw new IOException("Verification browser profile must not be a symbolic link.");
        File.SetUnixFileMode(profile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try { return new FileStream(profile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new IOException("A verification browser is already active for ffxiv_tc. Close it before signing in again."); }
    }
    public static async Task<string> GetTokenAsync(string regionRoot, CancellationToken token, Func<string, CancellationToken, Task<string>>? authenticate = null)
    {
        var browser = WebviewRuntime.FindBrowser();
        var profile = Path.Combine(regionRoot, "captcha-browser");
        using var profileLock = OpenProfile(profile);
        // CDP writes this anew; never connect to an endpoint left by an earlier run.
        var portFile = Path.Combine(profile, "DevToolsActivePort");
        File.Delete(portFile);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var ct = timeout.Token;
        var psi = new ProcessStartInfo(browser) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        psi.Environment.Remove("ELECTRON_RUN_AS_NODE");
        psi.Environment.Remove("NODE_OPTIONS");
        psi.Environment["LC_ALL"] = "C.UTF-8";
        psi.ArgumentList.Add(Path.Combine(Path.GetDirectoryName(browser)!, "resources/app"));
        foreach (var arg in new[] { "--user-data-dir=" + profile, "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1", "--no-first-run", "--no-default-browser-check", "--disable-extensions", "--app=about:blank" }) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new IOException("无法启动台服验证窗口。");
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            while (!File.Exists(portFile)) { if (process.HasExited) throw new IOException("台服验证窗口已关闭。"); await Task.Delay(100, ct); }
            var lines = await File.ReadAllLinesAsync(portFile, ct);
            if (!int.TryParse(lines[0], out var port)) throw new IOException("浏览器调试端口无效。");
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            using var targets = JsonDocument.Parse(await client.GetStringAsync("/json/list", ct));
            var target = targets.RootElement.EnumerateArray().First(t => t.GetProperty("type").GetString() == "page");
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(target.GetProperty("webSocketDebuggerUrl").GetString()!), ct);
            var pending = new ConcurrentDictionary<int, TaskCompletionSource<JsonElement>>();
            var events = Channel.CreateUnbounded<JsonElement>(); var commandId = 0;
            var reader = Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[65536];
                    while (!ct.IsCancellationRequested)
                    {
                        using var data = new MemoryStream(); WebSocketReceiveResult received;
                        do
                        {
                            received = await socket.ReceiveAsync(buffer, ct);
                            if (received.MessageType == WebSocketMessageType.Close) throw new IOException("台服验证窗口已关闭。");
                            data.Write(buffer, 0, received.Count);
                        } while (!received.EndOfMessage);
                        using var json = JsonDocument.Parse(data.ToArray()); var message = json.RootElement.Clone();
                        if (message.TryGetProperty("id", out var id) && pending.TryRemove(id.GetInt32(), out var completion))
                        {
                            if (message.TryGetProperty("error", out _)) completion.TrySetException(new IOException("浏览器验证命令失败。")); else completion.TrySetResult(message);
                        }
                        else await events.Writer.WriteAsync(message, ct);
                    }
                }
                catch (Exception ex) { foreach (var p in pending.Values) p.TrySetException(ex); events.Writer.TryComplete(ex); }
            }, ct);
            async Task<JsonElement> Send(string method, object parameters)
            {
                var id = ++commandId; var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously); pending[id] = completion;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct); return await completion.Task.WaitAsync(ct);
            }
            try
            {
                var binding = "superCaptcha" + Guid.NewGuid().ToString("N");
                await Send("Runtime.enable", new { });
                await Send("Runtime.addBinding", new { name = binding });
                await Send("Fetch.enable", new { patterns = new[] { new { urlPattern = PageUrl, resourceType = "Document", requestStage = "Request" } } });
                var navigation = Send("Page.navigate", new { url = PageUrl });
                await foreach (var message in events.Reader.ReadAllAsync(ct))
                {
                    var method = message.GetProperty("method").GetString(); var p = message.GetProperty("params");
                    if (method == "Fetch.requestPaused")
                    {
                        var html = "<!doctype html><html lang=zh-CN><meta charset=utf-8><title>" + System.Net.WebUtility.HtmlEncode(Localization.Display("FFXIV 台服验证")) + "</title>" +
                            "<body style='font:18px sans-serif;padding:32px;background:#181818;color:white'><h2>" + System.Net.WebUtility.HtmlEncode(Localization.Display("台服登录验证")) + "</h2><p id=s>" + System.Net.WebUtility.HtmlEncode(Localization.Display("正在执行官方网页验证…")) + "</p>" +
                            "<button onclick='location.reload()'>" + System.Net.WebUtility.HtmlEncode(Localization.Display("重试验证")) + "</button><script src='https://www.google.com/recaptcha/enterprise.js?render=" + SiteKey + "'></script><script>" +
                            "grecaptcha.enterprise.ready(async()=>{try{const t=await grecaptcha.enterprise.execute('" + SiteKey + "',{action:'LOGIN'});window['" + binding + "'](t);document.getElementById('s').textContent=" + JsonSerializer.Serialize(Localization.Display("验证完成，正在返回启动器")) + ";}catch(e){document.getElementById('s').textContent=" + JsonSerializer.Serialize(Localization.Display("验证未完成，请重试或关闭窗口取消。")) + ";}});</script></body></html>";
                        await Send("Fetch.fulfillRequest", new { requestId = p.GetProperty("requestId").GetString(), responseCode = 200,
                            responseHeaders = new[] { new { name = "Content-Type", value = "text/html; charset=utf-8" } }, body = Convert.ToBase64String(Encoding.UTF8.GetBytes(html)) });
                    }
                    else if (method == "Runtime.bindingCalled" && p.GetProperty("name").GetString() == binding)
                    {
                        var value = p.GetProperty("payload").GetString();
                        if (string.IsNullOrEmpty(value)) throw new IOException("网页验证没有返回有效令牌。");
                        await navigation;
                        // Keep the verification browser/session alive until the login exchange finishes,
                        // as xl_tw WebLoginWindow does. Never pass account credentials into the page.
                        var result = authenticate == null ? value : await authenticate(value, ct);
                        return result;
                    }
                }
                throw new IOException("台服网页验证未完成。");
            }
            finally
            {
                if (!process.HasExited)
                    try { await Send("Browser.close", new { }).WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception) { }
            }
        }
        finally
        {
            // Let Chromium flush its profile before falling back to forced cleanup.
            // The profile contains browser state only; credentials never enter the page.
            if (!process.HasExited)
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { }
            timeout.Cancel();
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
            File.Delete(portFile);
        }
    }
}
