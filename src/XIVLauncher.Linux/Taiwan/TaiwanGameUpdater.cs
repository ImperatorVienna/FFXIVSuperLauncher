using XIVLauncher.Linux.Patching;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace XIVLauncher.Linux.Taiwan;

// Protocol reference: xl_tw/Services/GameUpdateService.cs; shared ZiPatch comes from
// the GPL XL lineage. See compliance/provenance/taiwan-review.txt for scope and history.
public sealed class TaiwanGameUpdater(HttpClient client)
{
    public const string VersionBase = "http://patch-gamever.ffxiv.com.tw/http/win32/ffxivtc_release_tc_game/";
    internal static readonly Regex VersionPattern = Patching.GamePatchFiles.VersionPattern;
    public static string Version(string game, int repository = 0) => Patching.GamePatchFiles.Version(game, repository);
    public static string RequestBody(string game)
    {
        var body = new StringBuilder("\n");
        for (var i = 1; i <= 5; i++)
            if (File.Exists(Path.Combine(game, $"game/sqpack/ex{i}/ex{i}.ver"))) body.Append($"ex{i}\t{Version(game, i)}\n");
        return body.ToString();
    }
    public async Task<IReadOnlyList<GamePatch>> CheckAsync(string game, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, VersionBase + Version(game) + "/");
        request.Headers.Add("X-Hash-Check", "enabled");
        request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(RequestBody(game)));
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode == HttpStatusCode.NoContent) return [];
        var text = await response.Content.ReadAsStringAsync(token);
        var patches = Parse(text);
        if (patches.Count == 0 && !string.IsNullOrWhiteSpace(text)) throw new IOException("台服更新服务器返回无法识别的补丁清单，未认定为最新版本。");
        return patches;
    }
    public static IReadOnlyList<GamePatch> Parse(string text) =>
        GamePatchManifest.Parse(text, false, uri => uri.Host == "patch-dl.ffxiv.com.tw", "ffxiv_tc");
    internal static Task VerifyAsync(string path, GamePatch patch, CancellationToken token) => Patching.GamePatchFiles.VerifyAsync(path, patch, token);
    public async Task UpdateAsync(string game, string regionRoot, Action<string> progress, CancellationToken token)
    {
        var patches = await CheckAsync(game, token);
        await XIVLauncher.Linux.Patching.ZiPatchInstaller.InstallAsync(client, patches, game, regionRoot, progress, token);
        if ((await CheckAsync(game, token)).Count != 0) throw new IOException("台服更新后仍有补丁待安装，请重试。");
        progress("游戏已是最新版本。");
    }
}
