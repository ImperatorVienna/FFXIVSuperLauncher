using XIVLauncher.Linux.Patching;
using System.Collections.Concurrent;
using XIVLauncher.Common.Http;
using XIVLauncher.GamePatchV3.Update.Models;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class Release050Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super050-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact] public void NewRegionDoesNotOptIntoDalamud() => Assert.False(new RegionSettings().EnableDalamud);
    [Fact] public async Task DownloadsReportEachFileAndEndOnFailure()
    {
        var events = new ConcurrentQueue<DownloadProgress>();
        using (new DownloadScope(events.Enqueue, default))
        {
            await Task.WhenAll(Enumerable.Range(0, 2).Select(i => Task.Run(async () =>
            { await UpdateDownload.CopyAsync(new MemoryStream(new byte[64]), new MemoryStream(), $"file{i}", 64, default); })));
            using (var failure = new DownloadTransfer("failure", null)) failure.Add(7);
        }
        foreach (var group in events.GroupBy(e => e.Id))
        { Assert.True(group.First().Active); Assert.False(group.Last().Active); }
        Assert.Equal(3, events.Select(e => e.Id).Distinct().Count()); Assert.Null(DownloadScope.Current);
    }
    [Fact] public async Task CancelledDownloadsCloseTheirProgress()
    {
        var events = new List<DownloadProgress>(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        using (new DownloadScope(events.Add, cancel.Token))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpdateDownload.CopyAsync(new MemoryStream(new byte[20]), new MemoryStream(), "cancelled", 20, cancel.Token));
        Assert.False(events.Last().Active);
    }
    [Fact] public async Task DisabledDalamudDoesNotResolveAnyProvider()
    {
        var service = new RegionDalamudUpdates((_, _, _, _) => throw new Exception("Must not resolve"));
        Assert.Null(await service.UpdateAsync(LinuxSettings.Load(root), _ => { }, default));
    }
    [Fact] public async Task FailedRegionDoesNotOverwriteOtherRegionVersions()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv_tc";
        settings.Current.GamePath = root; settings.Current.ClientVersion = "before";
        settings.RegionProfiles["ffxiv_cn"].ClientVersion = "unrelated";
        var called = new List<string>();
        var service = new RegionUpdateService(region => { called.Add(region); return new FakeSource(true); });
        await Assert.ThrowsAsync<IOException>(() => service.UpdateGameAsync(settings, _ => { }, default));
        Assert.Equal(new[] { "ffxiv_tc" }, called); Assert.Equal("before", settings.Current.ClientVersion);
        Assert.Equal("unrelated", settings.RegionProfiles["ffxiv_cn"].ClientVersion);
    }
    [Fact] public async Task SuccessfulUpdateRecordsLocalVersionOnlyForSelectedRegion()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv_tc"; settings.Current.GamePath = root;
        Directory.CreateDirectory(Path.Combine(root, "game")); File.WriteAllText(Path.Combine(root, "game/ffxivgame.ver"), "2026.10.06.0000.0000");
        await new RegionUpdateService(_ => new FakeSource(false)).UpdateGameAsync(settings, _ => { }, default);
        var saved = LinuxSettings.Load(root); Assert.Equal("2026.10.06.0000.0000", saved.Current.ClientVersion);
        Assert.Equal(root, saved.Current.VersionGamePath); Assert.Empty(saved.RegionProfiles["ffxiv_cn"].ClientVersion);
    }
    [Fact] public void GlobalProviderIsIsolatedAndHasNoSubscriptionLink()
    {
        Assert.IsType<Global.GlobalLogin>(RegionLogin.For("ffxiv"));
        Assert.NotNull(RegionBackends.For("ffxiv"));
        Assert.NotNull(DalamudSources.For("ffxiv"));
        Assert.Null(OfficialContent.LinksFor("ffxiv").Subscription);
    }
    [Fact] public void OfficialNewsParsersKeepLinksAndDecodeText()
    {
        var tc = OfficialContent.ParseTaiwan("<a href='news_content.aspx?id=nav'>navigation</a><div class='title'><a href='news_content.aspx?id=abc'><span>活动 &amp; 更新</span></a></div><div class='title'><a href='javascript:news_content.aspx?id=x'>bad</a></div>");
        var item = Assert.Single(tc); Assert.Equal("活动 & 更新", item.Title); Assert.StartsWith("https://www.ffxiv.com.tw/", item.Url);
        var cn = OfficialContent.ParseChina("{\"Data\":[{\"Title\":\"活动\",\"Id\":123,\"OutLink\":\"javascript:bad\"}]}");
        Assert.EndsWith("/123", Assert.Single(cn).Url);
    }
    [Theory] [InlineData("../other")] [InlineData("../../game")] [InlineData("/etc/file")]
    public void SharedDalamudInstallerRejectsEscapingPaths(string path) => Assert.Throws<IOException>(() => DalamudUpdateService.SafeChild(root, path));
    [Fact] public void DiagnosticsSeparateEnglishTechnicalDetailsFromLocalizedMessage()
    { var text = DiagnosticLog.Format(new IOException("台服补丁 SHA1 校验失败。")); Assert.Contains("SHA1 verification failed", text); Assert.Contains("System.IO.IOException", text); Assert.DoesNotContain("中文", text); }
    [Fact] public void ClientVersionRefreshRescansAnUnchangedPath()
    {
        Directory.CreateDirectory(Path.Combine(root, "game")); var path = Path.Combine(root, "game/ffxivgame.ver");
        File.WriteAllText(path, "initial"); var profile = new RegionSettings { GamePath = root };
        VersionRecords.InitializeClient(profile); File.WriteAllText(path, "external-change"); VersionRecords.InitializeClient(profile);
        Assert.Equal("external-change", profile.ClientVersion);
        profile.GamePath = root + "/other"; VersionRecords.InitializeClient(profile); Assert.Empty(profile.ClientVersion);
    }
    private sealed class FakeSource(bool fail) : IRegionBackend
    {
        public Task UpdateGameAsync(DirectoryInfo game, string root, Action<string> progress, CancellationToken token) => fail ? Task.FromException(new IOException("Test failure")) : Task.CompletedTask;
        public Task<GameUpdateCheckResult> CheckGameAsync(DirectoryInfo game, CancellationToken token) => Task.FromResult(new GameUpdateCheckResult());
    }
}
