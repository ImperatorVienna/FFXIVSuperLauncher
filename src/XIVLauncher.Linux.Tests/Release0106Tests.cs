using System.Net;
using XIVLauncher.Linux.Taiwan;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release0106Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super0106-" + Guid.NewGuid());
    [Theory]
    [InlineData("steam",false)][InlineData("steam-launch-wrapper",false)][InlineData("xivlauncher-super",false)]
    [InlineData("pressure-vessel",false)][InlineData("ffxiv_dx11.exe",true)][InlineData("wineserver",true)]
    [InlineData("ffxivlauncher64.exe",true)][InlineData("services.exe",true)]
    public void SteamWrappersDoNotOwnWinePrefix(string name,bool expected) => Assert.Equal(expected,SteamPrefixDiscovery.IsPrefixOwner(name));
    private sealed class StalledHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
        { await Task.Delay(Timeout.Infinite,ct); return new(HttpStatusCode.OK); }
    }
    [Fact] public async Task TaiwanNetworkStallIsBoundedAndNeverRequestsSession()
    {
        using var http=new HttpClient(new StalledHandler()){Timeout=TimeSpan.FromMilliseconds(100)};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new TaiwanLogin(http).AuthenticateAsync("synthetic","test","123456","test",default).WaitAsync(TimeSpan.FromSeconds(3)));
    }
    [Theory][InlineData("en")][InlineData("ja")][InlineData("zh-TW")]
    public void EveryCatalogHasSameKeysAndPreservesPlaceholders(string language)
    {
        var source=Localization.Catalog("zh-CN");var target=Localization.Catalog(language);
        Assert.NotEmpty(source);Assert.Equal(source.Keys.Order(),target.Keys.Order());
        foreach(var (key,value) in target)
        {
            Assert.False(string.IsNullOrWhiteSpace(value));
            static string[] Slots(string text)=>System.Text.RegularExpressions.Regex.Matches(text,@"\{[^{}]+\}").Select(x=>x.Value).Order().ToArray();
            Assert.Equal(Slots(key),Slots(value));
        }
    }
    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        { await Task.Delay(Timeout.Infinite, token); return 0; }
    }
    [Fact] public async Task DownloadStallEndsProgressAndIsEligibleForDalamudFallback()
    {
        var reports = new List<XIVLauncher.Common.Http.DownloadProgress>();
        using var scope = new XIVLauncher.Common.Http.DownloadScope(reports.Add, default);
        using var input = new StalledStream(); using var output = new MemoryStream();
        var error = await Assert.ThrowsAsync<TimeoutException>(() => XIVLauncher.Common.Http.UpdateDownload.CopyAsync(input, output, "synthetic.zip", 100, default, TimeSpan.FromMilliseconds(100)).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(LocalDalamud.IsTimeout(error, default));
        Assert.Contains(reports, x => !x.Active && x.Succeeded == false);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.False(LocalDalamud.IsTimeout(error, canceled.Token));
    }
    [Fact] public void FirstRunUsesEnglishAndSavedLanguageSurvivesReload()
    {
        var settings = LinuxSettings.Load(root); Assert.Equal("en", settings.Language);
        settings.Language = "ja"; settings.Save(); Assert.Equal("ja", LinuxSettings.Load(root).Language);
    }
    [Fact] public void PresentationLocalizationDoesNotChangeDiagnosticTextOrLanguageNames()
    {
        try
        {
            Localization.SetLanguage("en");
            Assert.Equal("游戏", Localization.T("游戏")); // Log callers retain the original EnglishLog lookup key.
            Assert.Equal("Game", Localization.Display("游戏"));
            Assert.Equal("Currently managed game region: Global", Localization.Display("当前管理的游戏区服：国际区"));
            Assert.Equal("Found 3 Proton versions.", Localization.Display("已找到 3 个 Proton。"));
            Assert.Equal("日本語", Localization.Display("日本語"));
            Assert.Equal("unknown text", Localization.Display("unknown text"));
        }
        finally { Localization.SetLanguage("zh-CN"); }
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
