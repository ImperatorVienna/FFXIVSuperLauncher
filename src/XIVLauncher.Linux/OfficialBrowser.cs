using System.Diagnostics;
namespace XIVLauncher.Linux;
public static class OfficialBrowser
{
    public static void Open(string url, string root)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new IOException("官方页面地址无效。");
        var browser = WebviewRuntime.FindBrowser();
        var profile = Path.Combine(root, "web-sessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var start = new ProcessStartInfo(browser) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.Environment.Remove("ELECTRON_RUN_AS_NODE"); start.Environment.Remove("NODE_OPTIONS"); start.Environment["LC_ALL"] = "C.UTF-8";
        start.ArgumentList.Add(Path.Combine(Path.GetDirectoryName(browser)!, "resources/app"));
        start.ArgumentList.Add("--user-data-dir=" + profile); start.ArgumentList.Add("--official-url=" + uri.AbsoluteUri); start.ArgumentList.Add("--ui-language=" + Localization.Language);
        start.ArgumentList.Add("--toolbar-labels=" + System.Text.Json.JsonSerializer.Serialize(new[] { "后退", "前进", "重新加载", "在系统浏览器中打开" }.Select(Localization.Display)));
        var process = Process.Start(start) ?? throw new IOException("无法打开官方页面窗口。");
        _ = Task.Run(async () =>
        {
            try { await Task.WhenAll(process.StandardError.BaseStream.CopyToAsync(Stream.Null), process.StandardOutput.BaseStream.CopyToAsync(Stream.Null), process.WaitForExitAsync()); }
            finally { process.Dispose(); try { Directory.Delete(profile, true); } catch (IOException) { } }
        });
    }
}
