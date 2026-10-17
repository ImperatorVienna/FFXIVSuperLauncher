using XIVLauncher.Linux.Patching.ZiPatch.Util;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class ContainedPathTests
{
    [Theory]
    [InlineData("E:\\Games\\FFXIV Super Launcher\\ffxiv_cn", "E:\\Games\\FFXIV Super Launcher\\ffxiv_cn\\Dalamud.dll", true)]
    [InlineData("E:\\Games\\ffxiv_cn", "e:\\games\\FFXIV_CN\\addon\\file.dll", true)]
    [InlineData("E:\\Games\\ffxiv_cn", "E:\\Games\\ffxiv_cn-other\\file.dll", false)]
    [InlineData("E:\\", "E:\\file.dll", true)]
    [InlineData("E:\\Games", "E:\\Games\\", false)]
    public void WindowsBoundaryUsesBackslashAndCaseInsensitiveComparison(string root, string path, bool expected)
        => Assert.Equal(expected, Platforms.FileSystemPathRules.Windows.Contains(root, path));
    [Theory]
    [InlineData("../escape")][InlineData("..\\escape")][InlineData("C:\\outside.dll")][InlineData("file:stream")][InlineData(".")]
    public void BothInstallersRejectEscapes(string relative)
    {
        var root = Path.Combine(Path.GetTempPath(), "contained-path");
        Assert.Throws<IOException>(() => DalamudUpdateService.SafeChild(root, relative));
        Assert.Throws<IOException>(() => SqexFile.Resolve(root, relative));
    }
    [Fact]
    public void BothInstallersResolveValidNestedPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "游戏 & folder");
        var expected = Path.Combine(root, "sub", "file.dll");
        Assert.Equal(expected, DalamudUpdateService.SafeChild(root, "sub/file.dll"));
        Assert.Equal(expected, DalamudUpdateService.SafeChild(root, "sub\\file.dll"));
        Assert.Equal(expected, SqexFile.Resolve(root, "sub\\file.dll"));
    }
    [Fact]
    public void NativeFailureDiagnosticsKeepErrorCodeWithoutRawMessage()
    {
        var error = new IOException("secret outer", new System.ComponentModel.Win32Exception(5, "secret inner"));
        var diagnostic = Frontend.FrontendErrors.Diagnostic(error);
        Assert.Contains("Windows error code: 5", diagnostic);
        Assert.Contains("HRESULT:", diagnostic);
        Assert.DoesNotContain("secret", diagnostic);
    }
    [Theory]
    [InlineData("/data", "/data/file", true)]
    [InlineData("/data", "/Data/file", false)]
    [InlineData("/", "/file", true)]
    [InlineData("/data", "/data-other/file", false)]
    public void LinuxBoundariesRemainCaseSensitive(string root, string path, bool expected)
        => Assert.Equal(expected, Platforms.FileSystemPathRules.Linux.Contains(root, path));

    [Fact]
    public void SharedBoundaryRejectsSiblingAndLinkedParent()
    {
        var root = Path.Combine(Path.GetTempPath(), "path-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ContainedPath.Ensure(root, root);
            Assert.Throws<IOException>(() => ContainedPath.Ensure(root, root + "-other/file"));
            if (OperatingSystem.IsLinux())
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "linked"), Path.GetTempPath());
                Assert.Throws<IOException>(() => ContainedPath.Ensure(root, Path.Combine(root, "linked", "file")));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void LinuxPatchStreamsDoNotMergeDifferentlyCasedFiles()
    {
        if (!OperatingSystem.IsLinux()) return;
        var root = Path.Combine(Path.GetTempPath(), "patch-stream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var store = new SqexFileStreamStore();
            var upper = store.GetStream(Path.Combine(root, "File"), FileMode.OpenOrCreate, 1, 0);
            var lower = store.GetStream(Path.Combine(root, "file"), FileMode.OpenOrCreate, 1, 0);
            Assert.NotSame(upper, lower);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void PlatformToolsAndEnvironmentStaySeparated()
    {
        var root = Path.GetTempPath();
        var linux = new Platforms.Linux.LinuxEnvironment();
        var windows = new Platforms.Windows.WindowsEnvironment();
        Assert.Equal(Path.Combine(root, "Tools/webview/electron"), linux.BrowserPath(root));
        Assert.Equal(Path.Combine(root, "electron.exe"), windows.BrowserPath(root));
        Assert.Equal(Path.Combine(root, "Tools/xdelta3"), linux.PatchWorkerPath(root));
        Assert.Equal(Path.Combine(root, "Tools/xdelta3.exe"), windows.PatchWorkerPath(root));
        Assert.Null(windows.ReadProcessEnvironment(Environment.ProcessId));
    }
}
