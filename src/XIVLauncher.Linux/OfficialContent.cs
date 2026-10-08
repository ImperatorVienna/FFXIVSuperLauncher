using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using XIVLauncher.Common.Constant;
namespace XIVLauncher.Linux;

public sealed record OfficialLinks(string Home, string Shop, string? Subscription, string? MogStation = null);
public sealed record OfficialArticle(string Title, string Url);
public sealed record OfficialNews(IReadOnlyList<OfficialArticle> Activities, IReadOnlyList<OfficialArticle> Notices);
public static class OfficialContent
{
    public static OfficialLinks LinksFor(string region, string cdKeyVersion = "NA", string euShopLanguage = "en-gb") => region switch
    {
        "ffxiv_cn" => new("https://ff.web.sdo.com/web8/index.html", Links.SDO_SHOPPING_URL, Links.SDO_PAYMENT_URL),
        "ffxiv_tc" => new("https://www.ffxiv.com.tw/web/index.aspx", "https://www.ffxiv.com.tw/web/store/index.aspx", "https://www.ffxiv.com.tw/web/store/subscribe_step1.aspx"),
        "ffxiv" => InternationalWeb.Links(cdKeyVersion, euShopLanguage),
        _ => throw new NotSupportedException("未知区服。")
    };
    public static async Task<OfficialNews> LoadAsync(HttpClient client, string region, CancellationToken token, string cdKeyVersion = "NA", string euShopLanguage = "en-gb")
    {
        if (region == "ffxiv_cn")
        {
            var activities = client.GetStringAsync(Links.SDO_NEWS_BANNER_API_URL, token);
            var notices = client.GetStringAsync(Links.SDO_NEWS_LIST_API_URL, token);
            await Task.WhenAll(activities, notices);
            return new(ParseChina(await activities), ParseChina(await notices));
        }
        if (region == "ffxiv_tc")
        {
            var activities = client.GetStringAsync("https://www.ffxiv.com.tw/web/news/news_list.aspx?category=1", token);
            var notices = client.GetStringAsync("https://www.ffxiv.com.tw/web/news/news_list.aspx?category=2", token);
            await Task.WhenAll(activities, notices);
            return new(ParseTaiwan(await activities), ParseTaiwan(await notices));
        }
        if (region == "ffxiv")
        {
            var (site, _, lang) = InternationalWeb.Preferences(cdKeyVersion, euShopLanguage);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://frontier.ffxiv.com/news/headline.json?lang=" + lang + "&media=pcapp");
            request.Headers.TryAddWithoutValidation("User-Agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 0000000000)");
            request.Headers.Referrer = new Uri("https://launcher.finalfantasyxiv.com/");
            using var response = await client.SendAsync(request, token); response.EnsureSuccessStatusCode();
            return ParseGlobal(await response.Content.ReadAsStringAsync(token), site);
        }
        throw new NotSupportedException("Unknown game region.");
    }
    internal static OfficialNews ParseGlobal(string json, string site)
    {
        using var document = JsonDocument.Parse(json);
        IReadOnlyList<OfficialArticle> Read(string name, string path)
        {
            if (!document.RootElement.TryGetProperty(name, out var list)) return [];
            return list.EnumerateArray().Take(6).Select(item =>
            {
                var title = item.GetProperty("title").GetString() ?? "";
                var url = item.TryGetProperty("url", out var link) ? link.GetString() : null;
                if (string.IsNullOrEmpty(url) && item.TryGetProperty("id", out var id))
                    url = $"https://{site}.finalfantasyxiv.com/lodestone/{path}/detail/" + Uri.EscapeDataString(id.GetString() ?? "");
                return new OfficialArticle(WebUtility.HtmlDecode(title), url ?? "");
            }).Where(a => Uri.TryCreate(a.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host.EndsWith(".finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        return new(Read("topics", "topics"), Read("news", "news"));
    }
    internal static IReadOnlyList<OfficialArticle> ParseChina(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("Data").EnumerateArray().Take(6).Select(item =>
        {
            var link = item.TryGetProperty("OutLink", out var value) ? value.GetString() : null;
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                link = Links.SDO_NEWS_ARTICLE_BASE_URL + item.GetProperty("Id").ToString();
            return new OfficialArticle(WebUtility.HtmlDecode(item.GetProperty("Title").GetString() ?? ""), link!);
        }).ToArray();
    }
    internal static IReadOnlyList<OfficialArticle> ParseTaiwan(string html)
    {
        var results = new List<OfficialArticle>();
        foreach (Match match in Regex.Matches(html, "<div\\b[^>]*class=[\"']title[\"'][^>]*>\\s*<a\\b[^>]*href=[\"'](?<url>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)))
        {
            var url = new Uri(new Uri("https://www.ffxiv.com.tw/web/news/"), WebUtility.HtmlDecode(match.Groups["url"].Value));
            if (url.Host != "www.ffxiv.com.tw" || url.Scheme != "https") continue;
            var title = WebUtility.HtmlDecode(Regex.Replace(match.Groups["text"].Value, "<[^>]*>", " ", RegexOptions.None, TimeSpan.FromSeconds(1))).Trim();
            title = Regex.Replace(title, @"\s+", " ");
            if (title.Length > 0 && results.All(x => x.Url != url.AbsoluteUri)) results.Add(new(title, url.AbsoluteUri));
        }
        return results.Take(6).ToArray();
    }
}
