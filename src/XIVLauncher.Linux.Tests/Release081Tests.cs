using System.Text.Json;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release081Tests
{
    [Theory]
    [InlineData("JP", "de-de", "jp", "ja-jp")]
    [InlineData("NA", "fr-fr", "na", "en-us")]
    [InlineData("EU", "en-gb", "eu", "en-gb")]
    [InlineData("EU", "fr-fr", "fr", "fr-fr")]
    [InlineData("EU", "de-de", "de", "de-de")]
    [InlineData("EU", "unknown", "eu", "en-gb")]
    [InlineData("unknown", "de-de", "na", "en-us")]
    public void RoutesUseAccountVersionAndOnlyEuUsesShopLanguage(string version,string language,string site,string locale)
    {
        var links=OfficialContent.LinksFor("ffxiv",version,language);
        Assert.Equal($"https://{site}.finalfantasyxiv.com/lodestone/",links.Home);
        Assert.Equal($"https://store.finalfantasyxiv.com/ffxivstore/{locale}/",links.Shop);
        Assert.Contains("&lang="+locale,links.MogStation!);Assert.Null(links.Subscription);
        Assert.Equal("secure.square-enix.com",new Uri(links.MogStation!).Host);
    }
    [Fact] public void OtherRegionsHaveNoMogStationAndIgnoreWebsitePreferences()
    {
        foreach(var region in new[]{"ffxiv_cn","ffxiv_tc"})
        {Assert.Equal(OfficialContent.LinksFor(region),OfficialContent.LinksFor(region,"JP","fr-fr"));Assert.Null(OfficialContent.LinksFor(region).MogStation);}
    }
    [Fact] public void DefaultsUseNa()
    {
        Assert.Equal("NA",JsonSerializer.Deserialize<RegionSettings>("{}")!.CdKeyVersion);
        Assert.Equal("NA",JsonSerializer.Deserialize<SavedAccountSettings>("{}")!.CdKeyVersion);
    }
    [Fact] public void WizardChoiceWithoutAccountPersistsAndAccountSwitchRestoresIndependentChoices()
    {
        var root=Path.Combine(Path.GetTempPath(),"super081-"+Guid.NewGuid());
        try{
            var settings=LinuxSettings.Load(root);settings.SelectedRegion="ffxiv";
            settings.Current.CdKeyVersion="EU";settings.Current.EuShopLanguage="fr-fr";settings.Save();
            settings=LinuxSettings.Load(root);Assert.Equal("EU",settings.Current.CdKeyVersion);
            settings.Account="one";settings.Save();AccountProfiles.Select(settings.Current,"two");
            Assert.Equal("NA",settings.Current.CdKeyVersion);settings.Current.CdKeyVersion="JP";settings.Save();
            AccountProfiles.Select(settings.Current,"one");Assert.Equal("EU",settings.Current.CdKeyVersion);Assert.Equal("fr-fr",settings.Current.EuShopLanguage);
            settings.Current.CdKeyVersion="NA";settings.Save();settings=LinuxSettings.Load(root);
            Assert.Equal("fr-fr",settings.Current.EuShopLanguage);
            AccountProfiles.Select(settings.Current,"two");Assert.Equal("JP",settings.Current.CdKeyVersion);
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public void NativeClientLanguageLabelsKeepProtocolValues()
    {
        Assert.Equal(new[]{"日本語","English","Français","Deutsch"},GameClientLanguage.Choices.Select(x=>x.Name));
        Assert.Equal(new[]{0,1,3,2},GameClientLanguage.Choices.Select(x=>(int)x.Value));
    }
}
