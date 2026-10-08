using System.Net;
using System.Text.Json;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release0105Tests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    [Theory]
    [InlineData("JP", "fr-fr", "ja", "jp")]
    [InlineData("NA", "de-de", "en-us", "na")]
    [InlineData("EU", "en-gb", "en-gb", "eu")]
    [InlineData("EU", "fr-fr", "fr", "fr")]
    [InlineData("EU", "de-de", "de", "de")]
    [InlineData("EU", "invalid", "en-gb", "eu")]
    public async Task NewsSourceAndFallbackLinksFollowAccountWebsitePreferences(string version,string language,string newsLanguage,string site)
    {
        var calls=0;
        using var client=new HttpClient(new Handler(request=>
        {
            calls++;
            Assert.Equal("https://frontier.ffxiv.com/news/headline.json?lang="+newsLanguage+"&media=pcapp",request.RequestUri!.AbsoluteUri);
            return new(HttpStatusCode.OK){Content=new StringContent("{\"topics\":[{\"title\":\"Event\",\"id\":\"topic-id\"}],\"news\":[{\"title\":\"Notice\",\"id\":\"notice-id\"}]}")};
        }));
        var news=await OfficialContent.LoadAsync(client,"ffxiv",default,version,language);
        Assert.Equal(1,calls);
        Assert.Equal($"https://{site}.finalfantasyxiv.com/lodestone/topics/detail/topic-id",Assert.Single(news.Activities).Url);
        Assert.Equal($"https://{site}.finalfantasyxiv.com/lodestone/news/detail/notice-id",Assert.Single(news.Notices).Url);
        Assert.Equal($"https://{site}.finalfantasyxiv.com/lodestone/",OfficialContent.LinksFor("ffxiv",version,language).Home);
    }
    [Fact] public void LegacyAccountCodeIsNormalizedInProfilesAndSerialization()
    {
        const string oldJson="{\"CdKeyVersion\":\"US\"}";
        var profile=JsonSerializer.Deserialize<RegionSettings>(oldJson)!;
        var account=JsonSerializer.Deserialize<SavedAccountSettings>(oldJson)!;
        Assert.Equal("NA",profile.CdKeyVersion);Assert.Equal("NA",account.CdKeyVersion);
        Assert.Contains("\"CdKeyVersion\":\"NA\"",JsonSerializer.Serialize(profile));
        Assert.Contains("\"CdKeyVersion\":\"NA\"",JsonSerializer.Serialize(account));
        Assert.Equal(new[]{"JP","NA","EU"},InternationalWeb.CdKeyVersions);
    }
}
