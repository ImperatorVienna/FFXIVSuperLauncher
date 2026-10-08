using System.Net;
using System.Security.Cryptography;
using System.Text;
using XIVLauncher.Linux.Patching;
namespace XIVLauncher.Linux.Global;

public sealed class GlobalGameUpdater(HttpClient client)
{
    public const string BaseVersion = "2012.01.01.0000.0000";
    private static readonly string[] BootFiles = ["ffxivboot.exe", "ffxivboot64.exe", "ffxivlauncher64.exe", "ffxivupdater64.exe"];
    public static HttpClient CreateClient() => new(new SocketsHttpHandler { UseCookies = false, AutomaticDecompression = DecompressionMethods.All, ConnectTimeout = TimeSpan.FromSeconds(20) }) { Timeout = TimeSpan.FromSeconds(60) };
    private static HttpRequestMessage Request(HttpMethod method, string url)
    { var request = new HttpRequestMessage(method, url); request.Headers.TryAddWithoutValidation("User-Agent", "FFXIV PATCH CLIENT"); return request; }
    public async Task<IReadOnlyList<GamePatch>> CheckBootAsync(string game, CancellationToken token)
    {
        var version = File.ReadAllText(Path.Combine(game, "boot/ffxivboot.ver")).Trim(); ValidateVersion(version);
        using var request = Request(HttpMethod.Get, "http://patch-bootver.ffxiv.com/http/win32/ffxivneo_release_boot/" + version + "/?time=" + DateTime.UtcNow.ToString("yyyy-MM-dd-HH") + "-00");
        using var response = await client.SendAsync(request, token); response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(token), true);
    }
    public async Task UpdateBootAsync(string game, string root, Action<string> status, CancellationToken token)
    {
        var patches = await CheckBootAsync(game, token);
        await ZiPatchInstaller.InstallAsync(client, patches, game, root, status, token, boot: true, writeBackup: true);
        if (patches.Count > 0 && (await CheckBootAsync(game, token)).Count != 0) throw new IOException("ffxiv boot patches remain after updating.");
    }
    public async Task<string> RegisterAndUpdateAsync(string game, string root, GlobalSession session, Action<string> status, CancellationToken token)
    {
        using var request = Request(HttpMethod.Post, "https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/" + GamePatchFiles.Version(game) + "/" + Uri.EscapeDataString(session.Id));
        request.Headers.Add("X-Hash-Check", "enabled"); request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(VersionReport(game, session.Expansion)));
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.Conflict) throw new IOException("ffxiv boot verification failed. Repair the official boot files before retrying.");
        response.EnsureSuccessStatusCode();
        if (!response.Headers.TryGetValues("X-Patch-Unique-Id", out var ids) || ids.FirstOrDefault() is not { Length: > 0 } uid) throw new IOException("ffxiv patch server did not return a session identifier.");
        var patches = Parse(await response.Content.ReadAsStringAsync(token), false);
        if (patches.Count == 0) return uid;
        await ZiPatchInstaller.InstallAsync(client, patches, game, root, status, token, writeBackup: true);
        return uid; // xl launches with the UID issued alongside this patch list.
    }
    internal static string VersionReport(string game, int expansion)
    {
        var boot = File.ReadAllText(Path.Combine(game, "boot/ffxivboot.ver")).Trim(); ValidateVersion(boot);
        var hashes = BootFiles.Select(name => { using var file = File.OpenRead(Path.Combine(game, "boot", name)); return name + "/" + file.Length + "/" + Convert.ToHexString(SHA1.HashData(file)).ToLowerInvariant(); });
        var body = new StringBuilder(boot + "=" + string.Join(',', hashes) + "\n");
        for (var i = 1; i <= expansion; i++)
        {
            var file = Path.Combine(game, $"game/sqpack/ex{i}/ex{i}.ver");
            var version = File.Exists(file) ? File.ReadAllText(file).Trim() : BaseVersion; ValidateVersion(version);
            body.Append($"ex{i}\t{version}\n");
        }
        return body.ToString();
    }
    internal static bool Allowed(Uri uri) => uri.Scheme is "http" or "https" && (uri.Host == "patch-dl.ffxiv.com" || uri.Host.EndsWith(".patch-dl.ffxiv.com", StringComparison.OrdinalIgnoreCase));
    private static void ValidateVersion(string value) { if (!GamePatchFiles.VersionPattern.IsMatch(value)) throw new IOException("ffxiv client version is invalid."); }
    internal static IReadOnlyList<GamePatch> Parse(string text, bool boot) =>
        GamePatchManifest.Parse(text, boot, Allowed, "ffxiv");
}
