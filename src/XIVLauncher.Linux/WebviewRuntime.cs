namespace XIVLauncher.Linux;

/// <summary>Shared browser installation discovery, independent of any region authentication backend.</summary>
internal static class WebviewRuntime
{
    public static string FindBrowser() => FindBrowser(AppContext.BaseDirectory);
    internal static string FindBrowser(string baseDirectory)
    {
        var bundled = Path.Combine(baseDirectory, "Tools/webview/electron");
        if (File.Exists(bundled)) return bundled;
        throw new IOException("缺少随包网页验证组件，请重新安装完整发行包。");
    }
}
