using Xunit;
using XIVLauncher.Login.Client;
using XIVLauncher.Common.Unix;
using XIVLauncher.Login.Models;
namespace XIVLauncher.Linux.Tests;
public sealed class Release090Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super090-" + Guid.NewGuid());
    private LinuxSettings Settings(string region) { var s = LinuxSettings.Load(root); s.SelectedRegion = region; return s; }
    private static async Task SaveEditorAsync(string root,string region,string name,string password,string secret,bool autoOtp,bool remember,CancellationToken token)
    {
        var settings=LinuxSettings.Load(root);
        var draft=new CredentialDraft { Region=region,Name=name,Existing=settings.RegionProfiles[region].Accounts.ContainsKey(name),Password=password,Secret=secret,RememberChina=remember,Dirty=true };
        await CredentialDraftStore.SaveAsync(root,[draft],token);
        settings=LinuxSettings.Load(root);settings.SelectedRegion=region;AccountProfiles.Select(settings.Current,name);settings.Current.AutoOtp=autoOtp;settings.Save();
    }
    [Theory][InlineData("ffxiv")][InlineData("ffxiv_tc")]
    public async Task ManagerSavesReadsUpdatesAndRetainsBlankSecrets(string region)
    {
        await SaveEditorAsync(root,region,"one","password1","JBSWY3DPEHPK3PXP",true,false,default);
        var s=Settings(region);Assert.Equal("password1",await RegionCredentials.PasswordForLoginAsync(s,"",default));
        await SaveEditorAsync(root,region,"one","","",true,false,default);
        Assert.Equal("JBSWY3DPEHPK3PXP",await RegionCredentials.ReadAsync(Settings(region),"one","otp",default));
        await SaveEditorAsync(root,region,"two","password2","",false,false,default);
        s=Settings(region);AccountProfiles.Select(s.Current,"one");Assert.True(s.Current.AutoOtp);
        Assert.Equal("password1",await RegionCredentials.PasswordForLoginAsync(s,"",default));
        await SaveEditorAsync(root,region,"one","updated","",true,false,default);
        Assert.Equal("updated",await RegionCredentials.PasswordForLoginAsync(Settings(region),"",default));
        Assert.Null(await RegionCredentials.ReadAsync(root,region=="ffxiv"?"ffxiv_tc":"ffxiv","one","password",default));
    }
    [Fact] public async Task InvalidSecretAndMissingPasswordDoNotOverwriteSavedCredentials()
    {
        await SaveEditorAsync(root,"ffxiv","one","original","",false,false,default);
        await Assert.ThrowsAsync<CredentialValidationException>(()=>SaveEditorAsync(root,"ffxiv","one","changed","123456",true,false,default));
        await Assert.ThrowsAsync<CredentialValidationException>(()=>SaveEditorAsync(root,"ffxiv","one","","JBSWY3DPEHPK3PXP",true,false,default));
        Assert.Equal("original",await RegionCredentials.PasswordForLoginAsync(Settings("ffxiv"),"",default));Assert.False(Settings("ffxiv").Current.AutoOtp);
    }
    [Fact] public async Task ChinaManagerPreservesSessionAndRejectsPassword()
    {
        await SaveEditorAsync(root,"ffxiv_cn","one","","",false,true,default);
        var s=Settings("ffxiv_cn");await RegionCredentials.StoreAsync(s,"one","session","synthetic-session",default);
        await SaveEditorAsync(root,"ffxiv_cn","one","","",false,true,default);
        Assert.Equal("synthetic-session",await RegionCredentials.ReadAsync(Settings("ffxiv_cn"),"one","session",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>SaveEditorAsync(root,"ffxiv_cn","one","password","",false,true,default));
    }
    private string Prefix(string library,string id)
    {
        var data=Path.Combine(library,"steamapps","compatdata",id);Directory.CreateDirectory(Path.Combine(data,"pfx","drive_c"));File.WriteAllText(Path.Combine(data,"pfx","system.reg"),"synthetic");return data;
    }
    [Fact] public void SteamScanFindsBothEditionsInMultipleLibrariesAndNormalizesPfx()
    {
        var steam=Path.Combine(root,"Steam");var library=Path.Combine(root,"OtherLibrary");
        var full=Prefix(steam,"39210");var trial=Prefix(library,"312060");
        File.WriteAllText(Path.Combine(steam,"steamapps/libraryfolders.vdf"),"\"libraryfolders\" { \"1\" { \"path\" \""+library+"\" } }");
        var found=SteamPrefixDiscovery.Discover([steam]);Assert.Equal(2,found.Count);Assert.Contains(found,x=>x.DataDirectory==trial);
        Assert.Equal(full,SteamPrefixDiscovery.Normalize(Path.Combine(full,"pfx")));
        var alias=Path.Combine(root,"alias");Directory.CreateSymbolicLink(alias,full);Assert.Equal(full,SteamPrefixDiscovery.Normalize(alias));
        Assert.ThrowsAny<IOException>(()=>SteamPrefixDiscovery.Normalize(Path.Combine(root,"missing")));
    }
    [Fact] public void OnlyOptedInGlobalUsesSteamPrefixAndDisablingRestoresShared()
    {
        var s=Settings("ffxiv");var steam=Prefix(root,"39210");var proton=new ProtonInstallation("fake","/tmp/proton","/tmp/Steam",null,null);
        s.Current.SteamCompatibilityData=steam;s.Current.UseSteamPrefix=true;s.Save();
        Assert.Equal(steam,s.GetCompatibility(proton).DataDirectory);
        foreach(var region in new[]{"ffxiv_cn","ffxiv_tc"}){s.SelectedRegion=region;Assert.Equal(s.CompatibilityData,s.GetCompatibility(proton).DataDirectory);}
        s.SelectedRegion="ffxiv";s.Current.UseSteamPrefix=false;
        Assert.Equal(s.CompatibilityData,s.GetCompatibility(proton).DataDirectory);Assert.True(Directory.Exists(steam));
    }
    [Fact] public void SteamPrefixBusyProcessIsRejected()
    {
        var prefix=Prefix(root,"39210");
        var executable=Path.Combine(root,"wine64"); File.Copy("/usr/bin/sleep",executable);
        var start=new System.Diagnostics.ProcessStartInfo(executable,"30"){UseShellExecute=false};
        start.Environment["STEAM_COMPAT_DATA_PATH"]=prefix;
        using var process=System.Diagnostics.Process.Start(start)!;
        try { Assert.Throws<CredentialValidationException>(()=>SteamPrefixDiscovery.EnsureIdle(prefix)); }
        finally { process.Kill(); process.WaitForExit(); }
        SteamPrefixDiscovery.EnsureIdle(prefix);
    }
    [Fact] public async Task UnifiedDraftsKeepActiveRegionAndAccountAndIsolateSameNames()
    {
        var s=Settings("ffxiv_cn");s.Account="active";s.Save();
        await CredentialDraftStore.SaveAsync(root,[new(){Region="ffxiv_tc",Name="same",Password="tc",Dirty=true},new(){Region="ffxiv",Name="same",Password="global",Dirty=true}],default);
        s=LinuxSettings.Load(root);Assert.Equal("ffxiv_cn",s.SelectedRegion);Assert.Equal("active",s.Account);
        Assert.Equal("tc",await RegionCredentials.ReadAsync(root,"ffxiv_tc","same","password",default));
        Assert.Equal("global",await RegionCredentials.ReadAsync(root,"ffxiv","same","password",default));
        var drafts=CredentialDraftStore.Load(root);Assert.Equal(3,drafts.Count);Assert.All(drafts,d=>Assert.Equal("",d.Password));
    }
    [Fact] public async Task DraftClearAndDeleteOnlyTakeEffectWhenSaved()
    {
        await CredentialDraftStore.SaveAsync(root,[new(){Region="ffxiv",Name="one",Password="password",Secret="JBSWY3DPEHPK3PXP",Dirty=true}],default);
        var draft=CredentialDraftStore.Load(root).Single();draft.ClearOtp=true;draft.Dirty=true;
        Assert.NotNull(await RegionCredentials.ReadAsync(root,"ffxiv","one","otp",default));
        await CredentialDraftStore.SaveAsync(root,[draft],default);
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv","one","otp",default));
        Assert.Equal("password",await RegionCredentials.ReadAsync(root,"ffxiv","one","password",default));
        draft=CredentialDraftStore.Load(root).Single();draft.Delete=true;draft.Dirty=true;
        Assert.True(LinuxSettings.Load(root).RegionProfiles["ffxiv"].Accounts.ContainsKey("one"));
        await CredentialDraftStore.SaveAsync(root,[draft],default);
        Assert.False(LinuxSettings.Load(root).RegionProfiles["ffxiv"].Accounts.ContainsKey("one"));
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv","one","password",default));
    }
    [Fact] public async Task InvalidBatchDoesNotWriteEarlierDrafts()
    {
        var good=new CredentialDraft {Region="ffxiv_tc",Name="good",Password="password",Dirty=true};
        var bad=new CredentialDraft {Region="ffxiv",Name="bad",Secret="JBSWY3DPEHPK3PXP",Dirty=true};
        await Assert.ThrowsAsync<CredentialValidationException>(()=>CredentialDraftStore.SaveAsync(root,[good,bad],default));
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv_tc","good","password",default));
        Assert.False(File.Exists(Path.Combine(root,"linux-settings.json")));
    }
    [Theory][InlineData("ffxiv")][InlineData("ffxiv_tc")]
    public async Task MissingDisabledOrInvalidSavedOtpUsesManualPrompt(string region)
    {
        var s=Settings(region);s.Account="one";s.Current.AutoOtp=true;
        async Task Manual(){var invoked=false;var secret=await LoginCode.SavedSecretForLoginAsync(s,default);var value=await LoginCode.GetAsync(secret,_=>{invoked=true;return Task.FromResult("012345");},default);Assert.True(invoked);Assert.Equal("012345",value);}
        await Manual();
        await RegionCredentials.StoreAsync(s,"one","otp","invalid!",default);await Manual();
        await RegionCredentials.StoreAsync(s,"one","otp","JBSWY3DPEHPK3PXP",default);s.Current.AutoOtp=false;await Manual();
        s.Current.AutoOtp=true;Assert.NotNull(await LoginCode.SavedSecretForLoginAsync(s,default));
    }
    [Fact] public async Task ChinaNameOnlyRecordEnablesAutomaticQuickCredentialStorage()
    {
        await CredentialDraftStore.SaveAsync(root,[new(){Region="ffxiv_cn",Name="first",Dirty=true}],default);
        var s=LinuxSettings.Load(root);Assert.True(s.RegionProfiles["ffxiv_cn"].Accounts["first"].RememberPassword);
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv_cn","first","password",default));
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv_cn","first","session",default));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
