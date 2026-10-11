using System.Text.Json;
using XIVLauncher.Linux.Frontend;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class FrontendOfficialTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "frontend-official-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private void Select(string region, string version = "NA", string language = "en-gb") =>
        new FrontendPreferences(root).Update(new(Region: region, CdKeyVersion: version, EuShopLanguage: language));
    private static OfficialNews News(string title) => new([new(title, "https://www.ffxiv.com.tw/web/news/news_content.aspx?id=test", "2026-10-09")], []);
    private static async Task<OfficialView> Loaded(FrontendOfficial service)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true) { var value = service.Read(new()); if (value.State != "loading") return value; await Task.Delay(5, timeout.Token); }
    }

    [Fact] public async Task OldRegionResponseCannotReplaceCurrentNewsOrOpenItsArticles()
    {
        Select("ffxiv_cn");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new TaskCompletionSource<OfficialNews>(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = new List<string>();
        await using var service = new FrontendOfficial(root, (region, _, _, _) =>
        {
            if (region == "ffxiv_cn") { started.TrySetResult(); return old.Task; }
            return Task.FromResult(News("current TC news"));
        }, opened.Add);
        var first = service.Read(new()); Assert.Equal("loading", first.State);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Select("ffxiv_tc"); var current = await Loaded(service);
        old.SetResult(News("late CN response"));
        Assert.Equal("current TC news", Assert.Single(current.Activities).Title);
        Assert.Equal("ffxiv_tc", service.Read(new()).Region);
        Assert.Throws<ArgumentException>(() => service.Open(new(first.Key, "home")));
        service.Open(new(current.Key, current.Activities[0].Id));
        Assert.Single(opened);
        var previousId = current.Activities[0].Id;
        service.Read(new(true)); current = await Loaded(service);
        Assert.Throws<ArgumentException>(() => service.Open(new(current.Key, previousId)));
        Assert.Throws<ArgumentException>(() => service.Open(new(current.Key, "https://evil.example/")));
    }

    [Fact] public async Task UnresponsiveProviderTimesOutWhileLinksAndSettingsRemainUsable()
    {
        Select("ffxiv"); var opened = new List<string>();
        await using var service = new FrontendOfficial(root, (_, _, _, _) => new TaskCompletionSource<OfficialNews>().Task,
            opened.Add, TimeSpan.FromMilliseconds(30));
        var result = await Loaded(service);
        Assert.Equal("failed", result.State); Assert.Equal("timeout", result.Error);
        service.Open(new(result.Key, "status"));
        Assert.Equal("https://na.finalfantasyxiv.com/lodestone/worldstatus/", Assert.Single(opened));
        new FrontendPreferences(root).Update(new(Language: "ja"));
        Assert.Equal("ja", LinuxSettings.Load(root).Language);
        Assert.Contains(Directory.GetFiles(Path.Combine(root, "logs")), file => File.ReadAllText(file).Contains("Official content:"));
    }

    [Fact] public async Task RefreshRecoversFromFailureAndRejectsUnsafeArticleDestinations()
    {
        Select("ffxiv_tc"); var calls = 0;
        await using var service = new FrontendOfficial(root, (_, _, _, _) => ++calls == 1
            ? Task.FromException<OfficialNews>(new HttpRequestException("private response body"))
            : Task.FromResult(new OfficialNews([
                new("safe text <script>", "https://www.ffxiv.com.tw/web/news/"),
                new("unsafe", "javascript:alert(1)"), new("credentials", "https://user:password@www.ffxiv.com.tw/")], [])));
        Assert.Equal("failed", (await Loaded(service)).State);
        service.Read(new(true)); var result = await Loaded(service);
        Assert.Equal("ready", result.State); Assert.Single(result.Activities);
        Assert.Equal("safe text <script>", result.Activities[0].Title);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "logs"))) Assert.DoesNotContain("private response body", File.ReadAllText(file));
    }

    [Theory]
    [InlineData("JP", "en-gb", "jp", "ja")]
    [InlineData("NA", "en-gb", "na", "en-us")]
    [InlineData("EU", "en-gb", "eu", "en-gb")]
    [InlineData("EU", "fr-fr", "fr", "fr")]
    [InlineData("EU", "de-de", "de", "de")]
    public async Task GlobalPreferencesPersistAndRouteEveryEntry(string version, string language, string site, string newsLanguage)
    {
        Select("ffxiv", version, language); var opened = new List<string>();
        await using var service = new FrontendOfficial(root, (region, cdkey, eu, _) =>
        {
            Assert.Equal("ffxiv", region); Assert.Equal(version, cdkey); Assert.Equal(language, eu);
            return Task.FromResult(News("fixture"));
        }, opened.Add);
        var result = await Loaded(service);
        Assert.Equal("ffxiv:" + newsLanguage, result.Key);
        foreach (var id in result.Links) service.Open(new(result.Key, id));
        Assert.Contains($"https://{site}.finalfantasyxiv.com/lodestone/", opened);
        Assert.Contains($"https://{site}.finalfantasyxiv.com/lodestone/worldstatus/", opened);
        Assert.Equal(4, opened.Count); Assert.Contains("mog", result.Links); Assert.DoesNotContain("subscription", result.Links);
        Assert.Equal(language, LinuxSettings.Load(root).RegionProfiles["ffxiv"].EuShopLanguage);
    }

    [Fact] public async Task PublicContentDoesNotBlockCommandProcessingOrHostShutdown()
    {
        var output = new StringWriter();
        var input = new StringReader("""
            {"id":"news","method":"official.read","params":{}}
            {"id":"settings","method":"snapshot","params":{}}
            {"id":"invalid","method":"official.open","params":{"key":"ffxiv_cn","item":"home","url":"https://evil.example"}}
            """);
        await FrontendHost.RunAsync(root, input, output, fetchOfficial: (_, _, _, _) => new TaskCompletionSource<OfficialNews>().Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
        var values = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => JsonDocument.Parse(x).RootElement.Clone()).ToArray();
        Assert.Equal("loading", values[0].GetProperty("result").GetProperty("state").GetString());
        Assert.True(values[1].TryGetProperty("result", out _));
        Assert.Equal("invalid-request", values[2].GetProperty("error").GetProperty("code").GetString());
    }

    [Fact] public void DatesAreTakenFromPublisherAndMissingDatesStayEmpty()
    {
        var global = OfficialContent.ParseGlobal("""{"topics":[{"id":"x","title":"Title","date":"2026-10-09T03:00:00Z"}],"news":[{"id":"y","title":"Undated"}]}""", "na");
        Assert.Equal("2026-10-09", global.Activities[0].Date); Assert.Null(global.Notices[0].Date);
        Assert.Equal("2026-10-08", OfficialContent.ParseChina("""{"Data":[{"Id":1,"Title":"Title","PublishDate":"2026/10/08 18:35:01"}]}""")[0].Date);
        var tc = OfficialContent.ParseTaiwan("""<div class="title"><a href="/web/news/news_content.aspx?id=test">Title</a></div><div class="publish_date">2026/09/29</div>""");
        Assert.Equal("2026-09-29", Assert.Single(tc).Date);
    }
}
