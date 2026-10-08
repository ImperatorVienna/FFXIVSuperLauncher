using System.Text.Json.Nodes;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class RegionSettingsStorageTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"region-contract-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    [Theory][InlineData("ffxiv_cn")][InlineData("ffxiv_tc")][InlineData("ffxiv")]
    public void SaveWritesOnlyApplicableFieldsInRegionAndAccounts(string region)
    {
        var settings=LinuxSettings.Load(root);settings.SelectedRegion=region;settings.Account="synthetic";
        settings.Current.CdKeyVersion="EU";settings.Current.EuShopLanguage="de";settings.Current.AreaName="area";
        settings.Current.AutoOtp=true;settings.Save();
        var json=JsonNode.Parse(File.ReadAllText(Path.Combine(root,region,"settings.json")))!.AsObject();
        var account=json["Accounts"]!["synthetic"]!.AsObject();
        foreach(var node in new[]{json,account})
        {
            foreach(var name in new[]{"CdKeyVersion","EuShopLanguage","SteamAccount","FreeTrial"})Assert.Equal(region=="ffxiv",node.ContainsKey(name));
            foreach(var name in new[]{"AreaName"})Assert.Equal(region=="ffxiv_cn",node.ContainsKey(name));
            Assert.Equal(region!="ffxiv_cn",node.ContainsKey("AutoOtp"));
            Assert.False(node.ContainsKey("UseDesktopKeyring"));
        }
        Assert.Equal(region=="ffxiv",json.ContainsKey("ClientLanguage"));
        Assert.Equal(region=="ffxiv",json.ContainsKey("UseSteamPrefix"));
        Assert.Equal(region=="ffxiv_cn",json.ContainsKey("DeviceProfiles"));
        var restored=LinuxSettings.Load(root);
        Assert.Equal("synthetic",restored.Account);
        if(region=="ffxiv")Assert.Equal("EU",restored.Current.CdKeyVersion);
        Assert.False(json.ContainsKey("LoginMethod")); Assert.False(account.ContainsKey("LoginMethod"));
    }
    [Fact] public void OldForeignFieldsAreIgnoredButCredentialsAndNativePreferencesSurvive()
    {
        const string old="""{"CdKeyVersion":"EU","EuShopLanguage":"de","AreaName":"home","LoginMethod":2,"Accounts":{"a":{"CdKeyVersion":"JP","AreaName":"saved","LoginMethod":2,"CredentialProviders":{"session":false},"CredentialStores":[false]}}}""";
        var cn=RegionSettingsStorage.Deserialize("ffxiv_cn",old);
        Assert.Equal("home",cn.AreaName);Assert.DoesNotContain("LoginMethod",RegionSettingsStorage.Serialize("ffxiv_cn",cn));
        Assert.DoesNotContain("CredentialProviders",RegionSettingsStorage.Serialize("ffxiv_cn",cn));
        Assert.DoesNotContain("CdKeyVersion",RegionSettingsStorage.Serialize("ffxiv_cn",cn));
        var global=RegionSettingsStorage.Deserialize("ffxiv",old);
        Assert.Equal("EU",global.CdKeyVersion);Assert.Equal("JP",global.Accounts["a"].CdKeyVersion);
        Assert.DoesNotContain("AreaName",RegionSettingsStorage.Serialize("ffxiv",global));
    }
}
