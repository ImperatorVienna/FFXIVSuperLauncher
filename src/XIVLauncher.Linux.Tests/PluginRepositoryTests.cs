using Newtonsoft.Json.Linq;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class PluginRepositoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "repository-tests-" + Guid.NewGuid());
    private string Config => Path.Combine(root, "ffxiv_cn/dalamud/dalamudConfig.json");
    private void Seed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        File.WriteAllText(Config, new JObject { ["Untouched"] = 123, ["ThirdRepoList"] = new JObject { ["$type"] = "list-metadata", ["$values"] = new JArray(new JObject { ["Url"] = "https://example.test/catalog", ["IsEnabled"] = true, ["UnknownField"] = 7 }) } }.ToString());
    }
    [Fact] public void SavePreservesMetadataOtherSettingsAndBackup()
    {
        Seed(); var original = File.ReadAllText(Config); var read = PluginRepositories.Read(root, "ffxiv_cn"); Assert.True(read.PresetsEnabled);
        var result = PluginRepositories.Save(root, "ffxiv_cn", read.Revision, [new("https://example.test/catalog", false), new("https://second.test/catalog", true)], false, () => {});
        Assert.False(result.PresetsEnabled); Assert.Equal(2, result.Entries.Length);
        var saved = JObject.Parse(File.ReadAllText(Config)); Assert.Equal(123, saved.Value<int>("Untouched"));
        Assert.Equal("list-metadata", saved["ThirdRepoList"]!.Value<string>("$type"));
        Assert.Equal(7, saved["ThirdRepoList"]!["$values"]![0]!.Value<int>("UnknownField"));
        Assert.Equal(original, File.ReadAllText(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Config)!, "*.bak"))));
        Assert.False(Directory.Exists(Path.Combine(root, "ffxiv_tc")));
        Assert.Throws<CredentialValidationException>(() => PluginRepositories.Save(root, "ffxiv_cn", read.Revision, [], false, () => {}));
    }
    [Theory][InlineData("file:///tmp/list")][InlineData("https://user:password@example.test/list")]
    public void InvalidAddressDoesNotWrite(string url)
    {
        Seed(); var text = File.ReadAllText(Config); var read = PluginRepositories.Read(root, "ffxiv_cn");
        Assert.Throws<CredentialValidationException>(() => PluginRepositories.Save(root, "ffxiv_cn", read.Revision, [new(url, true)], false, () => {}));
        Assert.Equal(text, File.ReadAllText(Config));
    }
    [Fact] public void RunningGameAndDuplicateEntriesDoNotWrite()
    {
        Seed(); var text = File.ReadAllText(Config); var read = PluginRepositories.Read(root, "ffxiv_cn");
        Assert.Throws<IOException>(() => PluginRepositories.Save(root, "ffxiv_cn", read.Revision, [], false, () => throw new IOException("running")));
        Assert.Throws<ArgumentException>(() => PluginRepositories.Save(root, "ffxiv_cn", read.Revision, [read.Entries[0], read.Entries[0]], false, () => {}));
        Assert.Equal(text, File.ReadAllText(Config));
    }
    [Fact] public void SymlinkedConfigIsRejected()
    {
        Seed(); File.Move(Config, Config + ".original"); File.CreateSymbolicLink(Config, Config + ".original");
        Assert.Throws<IOException>(() => PluginRepositories.Read(root, "ffxiv_cn"));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
