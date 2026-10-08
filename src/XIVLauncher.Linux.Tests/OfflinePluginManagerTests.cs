using Newtonsoft.Json.Linq;
using XIVLauncher.Linux;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class OfflinePluginManagerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-plugin-" + Guid.NewGuid().ToString("N"));
    private const string Region = "ffxiv_cn";
    private readonly Guid id = Guid.NewGuid();
    private string ConfigPath => Path.Combine(root, Region, "dalamud/dalamudConfig.json");
    private string Plugin(string name = "Test", string version = "1.0", Guid? workingId = null, bool disabled = false, string region = Region)
    {
        var path = Path.Combine(root, region, "dalamud/installedPlugins", name, version); Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, name + ".dll"), "not executed");
        File.WriteAllText(Path.Combine(path, name + ".json"), new JObject { ["InternalName"] = name, ["Name"] = "Test plugin",
            ["WorkingPluginId"] = (workingId ?? id).ToString(), ["Disabled"] = disabled }.ToString());
        return path;
    }
    private JObject Entry(bool enabled, Guid? workingId = null) => new() { ["InternalName"] = "Test", ["WorkingPluginId"] = (workingId ?? id).ToString(), ["IsEnabled"] = enabled };
    private JObject Profile(params JObject[] entries) => new() { ["$type"] = "Dalamud.Plugin.Internal.Profiles.ProfileModelV1, Dalamud",
        ["e"] = true, ["p"] = 0, ["Plugins"] = new JObject { ["$type"] = "System.Collections.Generic.List`1[[Dalamud.Plugin.Internal.Profiles.ProfileModelV1+ProfileModelV1Plugin, Dalamud]], System.Private.CoreLib", ["$values"] = new JArray(entries) } };
    private void Config(JObject content) => File.WriteAllText(ConfigPath, content.ToString());
    private OfflinePluginList Read() => OfflinePluginManager.Read(root, Region);
    [Fact]
    public void RefreshReadsExternalStateWithoutWritingFiles()
    {
        Plugin(); Config(new JObject { ["DefaultProfile"] = Profile(Entry(true)) });
        Assert.True(Read().Plugins.Single().Enabled);
        Config(new JObject { ["DefaultProfile"] = Profile(Entry(false)) });
        var updated = File.ReadAllBytes(ConfigPath);
        Assert.False(Read().Plugins.Single().Enabled);
        Assert.Equal(updated, File.ReadAllBytes(ConfigPath));
    }
    [Fact]
    public void ApplyUpdatesOnlySelectedRegionAndPreservesUnknownSettingsAndBacksUp()
    {
        Plugin(); Plugin(region: "ffxiv_tc"); Plugin("Other", workingId: Guid.NewGuid());
        Config(new JObject { ["CustomSetting"] = new JObject { ["secret"] = "keep" }, ["DefaultProfile"] = Profile(Entry(true)) });
        var before = File.ReadAllText(ConfigPath); var snapshot = Read();
        var backup = OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = false }, snapshot.Snapshot);
        Assert.Equal(before, File.ReadAllText(Path.Combine(backup, "dalamudConfig.json")));
        Assert.False(Read().Plugins.Single(p => p.InternalName == "Test").Enabled);
        Assert.True(Read().Plugins.Single(p => p.InternalName == "Other").Enabled);
        Assert.True(OfflinePluginManager.Read(root, "ffxiv_tc").Plugins.Single().Enabled);
        Assert.Equal("keep", (string?)JObject.Parse(File.ReadAllText(ConfigPath))["CustomSetting"]?["secret"]);
        Assert.Contains("$values", File.ReadAllText(ConfigPath));
    }
    [Fact]
    public void DisableAllCollectionsAndEnableDespiteInactiveCollection()
    {
        Plugin(); var custom = Profile(Entry(true)); custom["e4c"] = true;
        Config(new JObject { ["DefaultProfile"] = Profile(), ["SavedProfiles"] = new JObject { ["$values"] = new JArray(custom) } });
        Assert.Null(Read().Plugins.Single().Enabled);
        OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = false }, Read().Snapshot);
        Assert.False(Read().Plugins.Single().Enabled);
        var saved = JObject.Parse(File.ReadAllText(ConfigPath));
        Assert.False((bool)saved["SavedProfiles"]!["$values"]![0]!["Plugins"]!["$values"]![0]!["IsEnabled"]!);
        OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = true }, Read().Snapshot);
        Assert.True(Read().Plugins.Single().Enabled);
        Assert.True((bool)JObject.Parse(File.ReadAllText(ConfigPath))["SavedProfiles"]!["$values"]![0]!["e4c"]!);
    }
    [Fact]
    public void LegacyPluginWithoutConfigUsesProfileNameMigration()
    {
        Plugin(workingId: Guid.Empty, disabled: true);
        Assert.False(Read().Plugins.Single().Enabled);
        OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = true }, Read().Snapshot);
        Assert.True(Read().Plugins.Single().Enabled);
        Assert.Contains("ProfileModelV1, Dalamud", File.ReadAllText(ConfigPath));
    }
    [Fact]
    public void ExplicitSelectionAppliesAfterInGameChangesAndPreservesNewSettings()
    {
        Plugin(); Config(new JObject { ["DefaultProfile"] = Profile(Entry(true)) });
        var displayed = Read();
        Config(new JObject { ["DefaultProfile"] = Profile(Entry(false)), ["NewInGameSetting"] = 42 });
        // The check remains true in the UI. Comparing only to its stale true snapshot was a no-op.
        OfflinePluginManager.ApplySelection(root, Region, displayed, new Dictionary<string, bool> { ["Test"] = true });
        Assert.True(Read().Plugins.Single().Enabled);
        Assert.Equal(42, (int)JObject.Parse(File.ReadAllText(ConfigPath))["NewInGameSetting"]!);
        OfflinePluginManager.ApplySelection(root, Region, displayed, new Dictionary<string, bool> { ["Test"] = false });
        Assert.False(Read().Plugins.Single().Enabled);
    }
    [Fact]
    public void ExplicitSelectionRefusesReinstalledPlugin()
    {
        Plugin(); var displayed = Read(); Plugin(workingId: Guid.NewGuid());
        Assert.Throws<IOException>(() => OfflinePluginManager.ApplySelection(root, Region, displayed, new Dictionary<string, bool> { ["Test"] = false }));
        Assert.False(File.Exists(ConfigPath));
    }
    [Fact]
    public void StaleSnapshotRefusesToOverwriteConfiguration()
    {
        Plugin(); Config(new JObject()); var snapshot = Read(); Config(new JObject { ["Changed"] = true });
        var before = File.ReadAllText(ConfigPath);
        Assert.Throws<IOException>(() => OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = false }, snapshot.Snapshot));
        Assert.Equal(before, File.ReadAllText(ConfigPath));
    }
    [Fact]
    public void StaleManifestAndUnknownPluginRefuseAllChanges()
    {
        Plugin(); var snapshot = Read(); Plugin(version: "2.0");
        Assert.Throws<IOException>(() => OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = false }, snapshot.Snapshot));
        Assert.Throws<IOException>(() => OfflinePluginManager.Apply(root, Region, new Dictionary<string, bool> { ["Test"] = false, ["Unknown"] = true }, Read().Snapshot));
        Assert.False(File.Exists(ConfigPath));
    }
    [Fact]
    public void UnknownProfileVersionIsNotRewritten()
    {
        Plugin(); Config(new JObject { ["DefaultProfile"] = new JObject { ["$type"] = "NewProfileV2" } });
        Assert.Throws<IOException>(() => Read());
    }
    [Fact]
    public void HighestVersionOnlyAndMalformedPluginDoesNotHideOtherPlugins()
    {
        Plugin(version: "1.0"); Plugin(version: "10.0", disabled: true);
        var broken = Plugin("Broken"); File.WriteAllText(Path.Combine(broken, "Broken.json"), "bad");
        var list = Read(); Assert.Single(list.Plugins); Assert.Single(list.Problems); Assert.Equal("10.0", list.Plugins[0].Version); Assert.False(list.Plugins[0].Enabled);
    }
    [Fact]
    public void SymlinkConfigIsRejected()
    {
        Plugin(); var outside = Path.Combine(root, "outside.json"); File.WriteAllText(outside, "{}");
        File.CreateSymbolicLink(ConfigPath, outside); Assert.Throws<IOException>(() => Read());
        Assert.Equal("{}", File.ReadAllText(outside));
    }
    [Fact]
    public void MultipleCheckboxChangesAreAppliedTogether()
    {
        Plugin(disabled: false); Plugin("Other", workingId: Guid.NewGuid(), disabled: true);
        var snapshot = Read();
        Assert.False(File.Exists(ConfigPath));
        OfflinePluginManager.Apply(root + "/", Region, new Dictionary<string, bool> { ["Test"] = false, ["Other"] = true }, snapshot.Snapshot);
        Assert.False(Read().Plugins.Single(p => p.InternalName == "Test").Enabled);
        Assert.True(Read().Plugins.Single(p => p.InternalName == "Other").Enabled);
    }
    [Fact]
    public void MissingPluginFolderIsEmptyAndReadDoesNotWrite()
    {
        Directory.CreateDirectory(root); Assert.Empty(Read().Plugins); Assert.Empty(Directory.GetFileSystemEntries(root));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
