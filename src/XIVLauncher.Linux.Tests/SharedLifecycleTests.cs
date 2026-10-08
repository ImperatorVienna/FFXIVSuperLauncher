using XIVLauncher.Linux.Patching;
using XIVLauncher.GamePatchV3.Update.Models;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class SharedLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-shared-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Theory]
    [InlineData("ffxiv_cn")][InlineData("ffxiv_tc")][InlineData("ffxiv")]
    public async Task ManualDalamudUpdateDoesNotEnableInjectionOrDependOnGameBackend(string region)
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = region; settings.EnableDalamud = false;
        var service = new RegionDalamudUpdates((id, path, _, _) =>
        {
            Assert.Equal(region, id); Assert.Equal(Path.Combine(root, region), path);
            return Task.FromResult(new RegionDalamudFiles(new FileInfo(Path.Combine(path, "addon/test/Dalamud.Injector.exe")), new(root), new(root), 0, region == "ffxiv_cn"));
        });
        await service.UpdateAsync(settings, _ => {}, default, manual: true);
        var saved = LinuxSettings.Load(root);
        Assert.False(saved.RegionProfiles[region].EnableDalamud);
        Assert.Equal("test", saved.RegionProfiles[region].DalamudVersion);
        Assert.All(saved.RegionProfiles.Where(x => x.Key != region), x => Assert.Empty(x.Value.DalamudVersion));
    }
    [Fact]
    public async Task FailedDalamudUpdateLeavesVersionAndPreferenceIntact()
    {
        var settings = LinuxSettings.Load(root); settings.EnableDalamud = true; settings.Current.DalamudVersion = "before";
        var service = new RegionDalamudUpdates((_, _, _, _) => Task.FromException<RegionDalamudFiles>(new IOException("offline")));
        await Assert.ThrowsAsync<IOException>(() => service.UpdateAsync(settings, _ => {}, default));
        Assert.Equal("before", settings.Current.DalamudVersion); Assert.True(settings.EnableDalamud);
    }
    [Fact]
    public void MalformedRegionReceiptDoesNotBreakOtherVersionRecords()
    {
        var settings = LinuxSettings.Load(root);
        foreach(var region in LinuxSettings.Regions)
        {
            var dir = Path.Combine(root, region.Id); Directory.CreateDirectory(Path.Combine(dir, "game"));
            settings.RegionProfiles[region.Id].GamePath = dir;
            File.WriteAllText(Path.Combine(dir, "game/ffxivgame.ver"), region.Id);
        }
        File.WriteAllText(Path.Combine(root, "ffxiv_cn/local-dalamud.json"), "{\"Version\":42}");
        VersionRecords.Refresh(settings);
        foreach(var region in LinuxSettings.Regions) Assert.Equal(region.Id, settings.RegionProfiles[region.Id].ClientVersion);
    }
    [Theory][InlineData("patch-dl.ffxiv.com.tw")][InlineData("patch-dl.ffxiv.com")]
    public void SharedManifestRejectsOtherRegionHostsAndOverflow(string host)
    {
        string Manifest(string length, string url) => $"{length}\t0\t0\t0\t2026.01.01.0000.0000\tsha1\t4\t{new string('a',40)}\t{url}";
        Assert.Single(GamePatchManifest.Parse(Manifest("4", "https://"+host+"/ex1/test.patch"),false,u=>u.Host==host,"test"));
        Assert.Throws<IOException>(()=>GamePatchManifest.Parse(Manifest("4", "https://unexpected.invalid/test.patch"),false,u=>u.Host==host,"test"));
        Assert.Throws<IOException>(()=>GamePatchManifest.Parse(Manifest(long.MaxValue.ToString(), "https://"+host+"/test.patch"),false,u=>u.Host==host,"test"));
    }
    private sealed class SwitchingBackend(Action change) : IRegionBackend
    {
        public Task UpdateGameAsync(DirectoryInfo game,string root,Action<string> status,CancellationToken token) { change(); return Task.CompletedTask; }
        public Task<GameUpdateCheckResult> CheckGameAsync(DirectoryInfo game,CancellationToken token) => throw new NotSupportedException();
    }
    [Fact]
    public async Task UpdateRecordsTheRequestedRegionEvenIfSelectionChanges()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion="ffxiv_tc"; settings.Current.GamePath=root;
        Directory.CreateDirectory(Path.Combine(root,"game")); File.WriteAllText(Path.Combine(root,"game/ffxivgame.ver"),"requested-version");
        await new RegionUpdateService(_=>new SwitchingBackend(()=>settings.SelectedRegion="ffxiv")).UpdateGameAsync(settings,_=>{},default);
        Assert.Equal("requested-version",settings.RegionProfiles["ffxiv_tc"].ClientVersion);
        Assert.Empty(settings.RegionProfiles["ffxiv"].ClientVersion);
    }
}
