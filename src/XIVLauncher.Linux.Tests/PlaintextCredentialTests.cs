using System.Text.Json.Nodes;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class PlaintextCredentialTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "plaintext-credentials-" + Guid.NewGuid());
    [Theory][InlineData("ffxiv_cn")][InlineData("ffxiv_tc")][InlineData("ffxiv")]
    public async Task OldStorageFlagsAreIgnoredAndRemovedOnSave(string region)
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = region; settings.Account = "same"; settings.Save();
        var globalPath = Path.Combine(root, "linux-settings.json");
        var global = JsonNode.Parse(File.ReadAllText(globalPath))!; global["CredentialStorageUseKeyring"] = true;
        File.WriteAllText(globalPath, global.ToJsonString());
        var regionPath = Path.Combine(root, region, "settings.json");
        var profile = JsonNode.Parse(File.ReadAllText(regionPath))!;
        profile["UseDesktopKeyring"] = true;
        profile["Accounts"]!["same"]!["UseDesktopKeyring"] = true;
        profile["Accounts"]!["same"]!["CredentialProviders"] = JsonNode.Parse("{\"password\":true}");
        File.WriteAllText(regionPath, profile.ToJsonString());
        settings = LinuxSettings.Load(root);
        Assert.Null(await RegionCredentials.ReadAsync(settings,"same","password",default));
        await RegionCredentials.StoreAsync(settings,"same","password","synthetic-password",default);
        await RegionCredentials.StoreAsync(settings,"same","otp","JBSWY3DPEHPK3PXP",default);
        settings = LinuxSettings.Load(root);
        Assert.Equal("synthetic-password",await RegionCredentials.ReadAsync(settings,"same","password",default));
        Assert.DoesNotContain("Keyring",File.ReadAllText(globalPath));
        Assert.DoesNotContain("CredentialProviders",File.ReadAllText(regionPath));
        Assert.DoesNotContain("Keyring",File.ReadAllText(regionPath));
        var path = Path.Combine(root,region,"credentials.json");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,File.GetUnixFileMode(path));
        Assert.Equal("synthetic-password",JsonNode.Parse(File.ReadAllText(path))!["same"]!["password"]!.GetValue<string>());
        await RegionCredentials.RemoveAsync(settings,"same","otp",default);
        Assert.Null(await RegionCredentials.ReadAsync(settings,"same","otp",default));
        await AccountProfiles.DeleteAsync(settings,"same",default);
        Assert.Null(await RegionCredentials.ReadAsync(settings,"same","password",default));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
