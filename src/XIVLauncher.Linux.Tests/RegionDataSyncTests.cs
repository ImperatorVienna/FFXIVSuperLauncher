using XIVLauncher.Linux;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class RegionDataSyncTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-sync-" + Guid.NewGuid().ToString("N"));
    public RegionDataSyncTests() => Directory.CreateDirectory(root);
    private string Write(string relative, string data)
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, data); return path;
    }
    public static IEnumerable<object[]> CategoriesAndRegions => Enum.GetValues<RegionSyncContent>()
        .SelectMany(category => LinuxSettings.Regions.Select(region => new object[] { category, region.Id }));

    [Theory]
    [MemberData(nameof(CategoriesAndRegions))]
    public void SyncReplacesOnlySelectedCategoryAndBacksUpBothTargets(RegionSyncContent category, string source)
    {
        var relative = RegionDataSync.RelativePath(category);
        var file = category == RegionSyncContent.DalamudSettings;
        foreach (var region in LinuxSettings.Regions)
        {
            Write(region.Id + "/settings.json", "account settings");
            Write(region.Id + "/addon/injector.exe", "injector");
            Write(region.Id + "/runtime/dotnet.exe", "runtime");
            Write(region.Id + "/assets/test", "assets");
            foreach (var other in Enum.GetValues<RegionSyncContent>().Where(x => x != category))
                Write(region.Id + "/" + RegionDataSync.RelativePath(other) + (other == RegionSyncContent.DalamudSettings ? "" : "/keep"), "untouched");
            Write(region.Id + "/" + relative + (file ? "" : "/" + (region.Id == source ? "new/plugin.dll" : "obsolete.dll")),
                region.Id == source ? "{\"new\":true}" : "{\"old\":true}");
        }
        var result = RegionDataSync.Synchronize(root + "/", source, LinuxSettings.Regions.Where(r => r.Id != source).Select(r => r.Id).ToArray(), category);
        Assert.Equal(2, result.Targets.Length);
        foreach (var target in result.Targets)
        {
            Assert.Equal("{\"new\":true}", File.ReadAllText(Path.Combine(root, target, relative, file ? "" : "new/plugin.dll")));
            var backupPath = Path.Combine(result.BackupDirectory, target, relative, file ? "" : "obsolete.dll");
            Assert.Equal("{\"old\":true}", File.ReadAllText(backupPath));
            if (!file) Assert.False(File.Exists(Path.Combine(root, target, relative, "obsolete.dll")));
        }
        foreach (var region in LinuxSettings.Regions)
        {
            Assert.Equal("account settings", File.ReadAllText(Path.Combine(root, region.Id, "settings.json")));
            Assert.Equal("injector", File.ReadAllText(Path.Combine(root, region.Id, "addon/injector.exe")));
            Assert.Equal("runtime", File.ReadAllText(Path.Combine(root, region.Id, "runtime/dotnet.exe")));
            Assert.Equal("assets", File.ReadAllText(Path.Combine(root, region.Id, "assets/test")));
            foreach (var other in Enum.GetValues<RegionSyncContent>().Where(x => x != category))
                Assert.Equal("untouched", File.ReadAllText(Path.Combine(root, region.Id, RegionDataSync.RelativePath(other), other == RegionSyncContent.DalamudSettings ? "" : "keep")));
        }
        Assert.Equal("{\"new\":true}", File.ReadAllText(Path.Combine(root, source, relative, file ? "" : "new/plugin.dll")));
        Assert.Empty(Directory.GetDirectories(root, ".plugin-sync-*"));
    }

    [Theory]
    [InlineData(RegionSyncContent.DalamudSettings)]
    [InlineData(RegionSyncContent.PluginSettings)]
    [InlineData(RegionSyncContent.InstalledPlugins)]
    public void FailureAfterFirstReplacementRollsBackBothTargets(RegionSyncContent category)
    {
        var relative = RegionDataSync.RelativePath(category) + (category == RegionSyncContent.DalamudSettings ? "" : "/plugin");
        Write("ffxiv_cn/" + relative, "{\"new\":true}");
        Write("ffxiv_tc/" + relative, "{\"target\":1}");
        Write("ffxiv/" + relative, "{\"target\":2}");
        Assert.Throws<IOException>(() => RegionDataSync.SynchronizeCore(root, "ffxiv_cn", new[] { "ffxiv_tc", "ffxiv" }, category, default,
            index => { if (index == 1) throw new IOException("Simulated second-target disk failure"); }));
        Assert.Equal("{\"target\":1}", File.ReadAllText(Path.Combine(root, "ffxiv_tc", relative)));
        Assert.Equal("{\"target\":2}", File.ReadAllText(Path.Combine(root, "ffxiv", relative)));
        Assert.Empty(Directory.GetDirectories(root, ".plugin-sync-*"));
    }
    [Fact]
    public void MissingSourceNeverDeletesTargets()
    {
        var target = Write("ffxiv_tc/dalamud/pluginConfigs/keep.json", "keep");
        Assert.Throws<IOException>(() => RegionDataSync.Synchronize(root, "ffxiv_cn", new[] { "ffxiv_tc", "ffxiv" }, RegionSyncContent.PluginSettings));
        Assert.Equal("keep", File.ReadAllText(target));
    }
    [Fact]
    public void InvalidDalamudJsonDoesNotOverwriteTargets()
    {
        Write("ffxiv_cn/dalamud/dalamudConfig.json", "corrupted");
        var target = Write("ffxiv_tc/dalamud/dalamudConfig.json", "{}");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => RegionDataSync.Synchronize(root, "ffxiv_cn", new[] { "ffxiv_tc", "ffxiv" }, RegionSyncContent.DalamudSettings));
        Assert.Equal("{}", File.ReadAllText(target));
    }
    [Fact]
    public void SymbolicLinksAreRejectedWithoutChangingExternalFiles()
    {
        Write("ffxiv_cn/dalamud/pluginConfigs/config.json", "new");
        var external = Path.Combine(root, "external"); Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "keep"), "keep");
        Directory.CreateDirectory(Path.Combine(root, "ffxiv_tc/dalamud"));
        Directory.CreateSymbolicLink(Path.Combine(root, "ffxiv_tc/dalamud/pluginConfigs"), external);
        Assert.Throws<IOException>(() => RegionDataSync.Synchronize(root, "ffxiv_cn", new[] { "ffxiv_tc", "ffxiv" }, RegionSyncContent.PluginSettings));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(external, "keep")));
        Assert.Single(Directory.GetFiles(external));
    }
    [Fact]
    public void CancellationDoesNotChangeData()
    {
        Write("ffxiv_cn/dalamud/pluginConfigs/config", "new");
        var target = Write("ffxiv_tc/dalamud/pluginConfigs/config", "old");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => RegionDataSync.Synchronize(root, "ffxiv_cn", new[] { "ffxiv_tc", "ffxiv" }, RegionSyncContent.PluginSettings, cancelled.Token));
        Assert.Equal("old", File.ReadAllText(target));
    }
    [Fact]
    public void PluginHelperExecutablesStayExecutable()
    {
        var executable = Write("ffxiv_cn/dalamud/installedPlugins/helper", "binary");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        RegionDataSync.Synchronize(root, "ffxiv_cn", new[] { "ffxiv_tc", "ffxiv" }, RegionSyncContent.InstalledPlugins);
        Assert.True(File.GetUnixFileMode(Path.Combine(root, "ffxiv_tc/dalamud/installedPlugins/helper")).HasFlag(UnixFileMode.UserExecute));
    }
    [Theory]
    [MemberData(nameof(CategoriesAndRegions))]
    public void SingleDestinationLeavesUnselectedRegionAndSourceUntouched(RegionSyncContent category, string source)
    {
        var others = LinuxSettings.Regions.Where(r => r.Id != source).Select(r => r.Id).ToArray();
        var relative = RegionDataSync.RelativePath(category) + (category == RegionSyncContent.DalamudSettings ? "" : "/config");
        var sourcePath = Write(source + "/" + relative, "{\"new\":true}");
        Write(others[0] + "/" + relative, "{\"old\":true}");
        var untouched = Write(others[1] + "/" + relative, "{\"keep\":true}");
        var result = RegionDataSync.Synchronize(root, source, new[] { others[0] }, category);
        Assert.Equal(new[] { others[0] }, result.Targets);
        Assert.Equal("{\"new\":true}", File.ReadAllText(sourcePath));
        Assert.Equal("{\"new\":true}", File.ReadAllText(Path.Combine(root, others[0], relative)));
        Assert.Equal("{\"keep\":true}", File.ReadAllText(untouched));
        Assert.False(Directory.Exists(Path.Combine(result.BackupDirectory, others[1])));
        Assert.Equal("{\"old\":true}", File.ReadAllText(Path.Combine(result.BackupDirectory, others[0], relative)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ffxiv_cn")]
    [InlineData("ffxiv_tc,ffxiv_tc")]
    [InlineData("../outside")]
    public void InvalidDestinationsFailBeforeAnyWrites(string targetList)
    {
        var targets = targetList.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var source = Write("ffxiv_cn/dalamud/pluginConfigs/config", "source");
        var target = Write("ffxiv_tc/dalamud/pluginConfigs/config", "target");
        Assert.Throws<ArgumentException>(() => RegionDataSync.Synchronize(root, "ffxiv_cn", targets, RegionSyncContent.PluginSettings));
        Assert.Equal("source", File.ReadAllText(source));
        Assert.Equal("target", File.ReadAllText(target));
        Assert.False(Directory.Exists(Path.Combine(root, "sync-backups")));
        Assert.False(File.Exists(Path.Combine(root, ".plugin-sync.lock")));
    }

    public void Dispose() => Directory.Delete(root, true);
}
