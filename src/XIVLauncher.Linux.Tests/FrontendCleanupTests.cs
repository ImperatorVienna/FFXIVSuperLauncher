using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class FrontendCleanupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "launcher-cleanup-" + Guid.NewGuid());
    private string Put(string relative)
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test"); return path;
    }
    [Fact]
    public void DownloadsRemoveStagingButKeepInstalledFilesAndSymlinkTargets()
    {
        var id = Guid.NewGuid().ToString("N");
        var paths = new[] { "ffxiv_cn/patches/patch", "ffxiv/patches/patch", "ffxiv_tc/patches/patch",
            $".plugin-update-{id}/download.zip", $"ffxiv_cn/addon/Hooks/1.{id}.7z",
            $"ffxiv/assets/1.{id}/resource", $"ffxiv_tc/runtime.download-{id}", $"launcher.AppImage.download-{id}" };
        foreach (var path in paths) Put(path);
        var installed = Put("ffxiv_cn/addon/Hooks/1/Dalamud.dll");
        var plugin = Put("ffxiv_cn/dalamud/installedPlugins/Plugin/1/Plugin.dll");
        var settings = Put("ffxiv_cn/settings.json");
        var outside = Put("unrelated/keep");
        File.CreateSymbolicLink(Path.Combine(root, "ffxiv_cn/patches/link"), outside);
        var result = FrontendCleanup.Clean(root, "downloads", Path.Combine(root,"launcher.AppImage"), default);
        foreach (var path in paths) Assert.False(File.Exists(Path.Combine(root,path)));
        foreach (var path in new[] { installed, plugin, settings, outside }) Assert.True(File.Exists(path));
        Assert.True(result.Deleted >= paths.Length); Assert.True(result.Skipped > 0);
    }
    [Fact]
    public void LogsKeepDalamudAndPluginLogsAndDownloads()
    {
        var log = Put("logs/launcher-20260101-2147483647.log");
        var dalamud = Put("ffxiv_cn/logs/dalamud.log");
        var plugin = Put("logs/plugin.log");
        var patch = Put("ffxiv_cn/patches/patch");
        var result = FrontendCleanup.Clean(root, "logs", "", default);
        Assert.False(File.Exists(log));
        foreach (var path in new[] { dalamud, plugin, patch }) Assert.True(File.Exists(path));
        Assert.Equal(1, result.Deleted); Assert.Equal(4, result.Bytes);
    }
    [Fact]
    public void CancellationAndUnknownKindDoNotDeleteFiles()
    {
        var patch = Put("ffxiv_cn/patches/patch");
        Assert.Throws<ArgumentException>(() => FrontendCleanup.Clean(root, "all", "", default));
        Assert.Throws<OperationCanceledException>(() => FrontendCleanup.Clean(root, "downloads", "", new CancellationToken(true)));
        Assert.True(File.Exists(patch));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }
}
