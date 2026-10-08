using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class OptionalServicesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-optional-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public void MissingBrowserDoesNotRequireOrModifyRegionSettings()
    {
        Directory.CreateDirectory(root);
        Assert.Throws<IOException>(() => WebviewRuntime.FindBrowser(root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        var browser = Path.Combine(root, "Tools/webview/electron");
        Directory.CreateDirectory(Path.GetDirectoryName(browser)!);
        File.WriteAllText(browser, "fixture");
        Assert.Equal(browser, WebviewRuntime.FindBrowser(root));
    }

    [Fact]
    public void DiagnosticStorageFailureDoesNotInterruptCallerAndCanRecover()
    {
        Directory.CreateDirectory(root);
        var logs = Path.Combine(root, "logs");
        File.WriteAllText(logs, "blocked");
        var diagnostics = new SessionDiagnostics(root);
        diagnostics.Write("first", "ffxiv", "INFO");
        Assert.NotNull(diagnostics.WriteError);
        File.Delete(logs);
        diagnostics.Write("second", "ffxiv", "INFO");
        Assert.Contains("second", File.ReadAllText(diagnostics.FilePath));
    }
}
