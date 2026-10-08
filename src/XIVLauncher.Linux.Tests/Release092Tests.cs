using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release092Tests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"super092-"+Guid.NewGuid());
    [Fact] public async Task PlaintextPersistsAcrossRegions()
    {
        await CredentialDraftStore.SaveAsync(root,[],default);
        foreach(var region in new[]{"ffxiv_cn","ffxiv_tc","ffxiv"})
        {
            await CredentialDraftStore.SaveAsync(root,[new(){Region=region,Name="account",Password=region=="ffxiv_cn"?"":"password",Dirty=true}],default);
            var s=LinuxSettings.Load(root);s.SelectedRegion=region;AccountProfiles.Select(s.Current,"account");
            if(region=="ffxiv_cn")await RegionCredentials.StoreAsync(s,"account","session","quick",default);
            Assert.Equal(region=="ffxiv_cn"?"quick":"password",await RegionCredentials.ReadAsync(root,region,"account",region=="ffxiv_cn"?"session":"password",default));
        }
        await CredentialDraftStore.SaveAsync(root,[],default);
        Assert.Equal("password",await RegionCredentials.ReadAsync(root,"ffxiv","account","password",default));
    }
    [Fact] public async Task EditingCredentialsDoesNotOverwriteLoginAutoOtpChoiceAndClearedSecretIsRemoved()
    {
        await CredentialDraftStore.SaveAsync(root,[new(){Region="ffxiv",Name="one",Password="password",Secret="JBSWY3DPEHPK3PXP",Dirty=true}],default);
        var s=LinuxSettings.Load(root);s.SelectedRegion="ffxiv";AccountProfiles.Select(s.Current,"one");s.Current.AutoOtp=true;s.Save();
        var draft=CredentialDraftStore.Load(root).Single();draft.Password="updated";draft.Dirty=true;
        await CredentialDraftStore.SaveAsync(root,[draft],default);
        s=LinuxSettings.Load(root);Assert.True(s.Current.AutoOtp);
        draft=CredentialDraftStore.Load(root).Single();draft.ClearOtp=true;draft.Dirty=true;
        await CredentialDraftStore.SaveAsync(root,[draft],default);
        s=LinuxSettings.Load(root);Assert.True(s.Current.AutoOtp);Assert.Null(await LoginCode.SavedSecretForLoginAsync(s,default));
        Assert.Equal("updated",await RegionCredentials.PasswordForLoginAsync(s,"",default));
    }
    [Fact] public async Task SavingEmptyDraftDoesNotChangeCredentialsOrActiveRegion()
    {
        await CredentialDraftStore.SaveAsync(root,[new(){Region="ffxiv_tc",Name="one",Password="password",Dirty=true}],default);
        var path=Path.Combine(root,"ffxiv_tc","credentials.json");var original=File.ReadAllText(path);
        await CredentialDraftStore.SaveAsync(root,[],default);
        Assert.Equal(original,File.ReadAllText(path));Assert.Equal("ffxiv_cn",LinuxSettings.Load(root).SelectedRegion);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
