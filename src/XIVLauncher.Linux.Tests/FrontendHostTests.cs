using System.Text.Json;
using XIVLauncher.Linux.Frontend;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class FrontendHostTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "frontend-host-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }

    private async Task<JsonElement[]> Requests(params string[] lines)
    {
        var output = new StringWriter();
        Assert.Equal(0, await FrontendHost.RunAsync(root,new StringReader(string.Join('\n',lines)),output));
        return output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    }

    [Fact] public async Task SnapshotDoesNotCreateConfigurationOrExposeSecrets()
    {
        var first = await Requests("""{"id":"1","method":"snapshot","params":{}}""");
        Assert.False(Directory.Exists(root));
        Assert.Equal(5,first[0].GetProperty("result").GetProperty("protocolVersion").GetInt32());
        var settings=LinuxSettings.Load(root); settings.Account="synthetic"; settings.Save();
        await RegionCredentials.StoreValueAsync(root,"ffxiv_cn","synthetic","session","secret-test-session",default);
        var response=await Requests("""{"id":"2","method":"snapshot","params":{}}""");
        Assert.DoesNotContain("secret-test-session",response[0].ToString());
        Assert.DoesNotContain("synthetic",response[0].ToString());
    }

    [Fact] public async Task PreferencesPersistWithoutRewritingOtherFiles()
    {
        var settings=LinuxSettings.Load(root); settings.Save();
        var regionFile=Path.Combine(root,"ffxiv_cn","settings.json");
        File.AppendAllText(regionFile,"\n\n"); var before=File.ReadAllBytes(regionFile);
        var response=await Requests("""{"id":"1","method":"preferences.update","params":{"language":"ja","region":"ffxiv_tc"}}""");
        Assert.False(response[0].TryGetProperty("error",out _));
        var stored=LinuxSettings.Load(root);
        Assert.Equal("ja",stored.Language);Assert.Equal("ffxiv_tc",stored.SelectedRegion);
        Assert.Equal(before,File.ReadAllBytes(regionFile));
    }

    [Theory]
    [InlineData("{\"language\":\"fr\",\"region\":\"ffxiv\"}")]
    [InlineData("{\"language\":\"ja\",\"region\":\"../../wrong\"}")]
    [InlineData("{\"password\":\"do-not-echo\"}")]
    [InlineData("{}")]
    public async Task InvalidPreferencesDoNotWriteAndHostRecovers(string parameters)
    {
        var results=await Requests("{\"id\":\"1\",\"method\":\"preferences.update\",\"params\":"+parameters+"}",
            """{"id":"2","method":"snapshot","params":{}}""");
        Assert.Equal("invalid-request",results[0].GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("do-not-echo",results[0].ToString());
        Assert.True(results[1].TryGetProperty("result",out _));Assert.False(Directory.Exists(root));
    }

    [Fact] public async Task MalformedInputAndUnknownMethodDoNotTerminateHost()
    {
        var results=await Requests("not json", """{"id":"2","method":"execute","params":{}}""",
            """{"id":"3","method":"snapshot","params":{}}""");
        Assert.Equal(3,results.Length);Assert.True(results[2].TryGetProperty("result",out _));
        Assert.False(Directory.Exists(root));
    }

    [Fact] public async Task CorruptConfigurationReturnsFailureWithoutOverwritingIt()
    {
        Directory.CreateDirectory(root);var file=Path.Combine(root,"linux-settings.json");
        File.WriteAllText(file,"unreadable-settings");
        var result=await Requests("""{"id":"1","method":"snapshot","params":{}}""");
        Assert.True(result[0].TryGetProperty("error",out _));Assert.Equal("unreadable-settings",File.ReadAllText(file));
    }
}
