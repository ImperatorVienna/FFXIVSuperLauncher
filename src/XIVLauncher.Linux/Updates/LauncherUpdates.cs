using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace XIVLauncher.Linux.Updates;

public sealed record UpdateDistribution
{
    public string Kind { get; init; } = "archive";
    public string Channel { get; init; } = "stable";
    public string PublicKey { get; init; } = "";
    public string Api { get; init; } = "https://api.github.com/repos/ImperatorVienna/FFXIVSuperLauncher/releases?per_page=100";
    public bool LocalTest { get; init; }
    public static UpdateDistribution Load()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "distribution.json");
        var config = File.Exists(file) ? JsonSerializer.Deserialize<UpdateDistribution>(File.ReadAllText(file)) ?? new() : new();
        if (config.LocalTest && config.Channel == "preview" && Environment.GetEnvironmentVariable("SUPER_UPDATE_TEST_API") is { Length: > 0 } api)
        { var uri = new Uri(api); if (!uri.IsLoopback || uri.Scheme != "http") throw new IOException("Test override must be a local HTTP server."); config = config with { Api = api }; }
        return config;
    }
    public void ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0) throw new IOException("Invalid update URL.");
        if (LocalTest && Channel == "preview" && uri.Scheme == "http" && uri.IsLoopback) return;
        if (uri.Scheme != "https" || !uri.IsDefaultPort ||
            !(uri.Host == "api.github.com" && uri.AbsolutePath.StartsWith("/repos/ImperatorVienna/FFXIVSuperLauncher/releases", StringComparison.Ordinal) ||
              uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/ImperatorVienna/FFXIVSuperLauncher/releases/", StringComparison.Ordinal)))
            throw new IOException("Update URL is outside the configured release repository.");
    }
}
public sealed record LauncherRelease(Version Version, string Page, string? Manifest, string? Signature);
public sealed record SignedUpdate(string Version, string Channel, string Url, string Sha256, long Size);

// Independent of settings, credentials, game patching and Dalamud services.
public sealed class LauncherUpdates(HttpClient http, UpdateDistribution distribution)
{
    public static Version? ParseVersion(string tag) => Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") && Version.TryParse(tag.TrimStart('v'), out var version) ? version : null;
    public async Task<LauncherRelease?> CheckAsync(CancellationToken token)
    {
        distribution.ValidateUrl(distribution.Api);
        using var request = new HttpRequestMessage(HttpMethod.Get, distribution.Api);
        request.Headers.UserAgent.ParseAdd("FFXIVSuperLauncher/1.0");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null; // Empty/private repository: no anonymous release available.
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await ReadBoundedAsync(response, 4 * 1024 * 1024, token));
        LauncherRelease? best = null;
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || (distribution.Channel != "preview" && release.GetProperty("prerelease").GetBoolean())) continue;
            var version = ParseVersion(release.GetProperty("tag_name").GetString() ?? "");
            if (version == null || best != null && version <= best.Version) continue;
            var page = release.GetProperty("html_url").GetString()!; distribution.ValidateUrl(page);
            string? Find(string name) => release.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == name) is var a && a.ValueKind != JsonValueKind.Undefined ? a.GetProperty("browser_download_url").GetString() : null;
            best = new(version, page, Find("appimage-update.json"), Find("appimage-update.sig"));
        }
        return best;
    }
    private async Task<byte[]> FetchAsync(string url, int maximum, CancellationToken token)
    {
        distribution.ValidateUrl(url);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        return await ReadBoundedAsync(response, maximum, token);
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, int maximum, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > maximum) throw new IOException("Update metadata exceeds the size limit.");
        using var output = new MemoryStream(); using var stream = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0)
        { if (output.Length + read > maximum) throw new IOException("Update metadata exceeds the size limit."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    public async Task<SignedUpdate> VerifyAsync(LauncherRelease release, CancellationToken token)
    {
        if (release.Manifest == null || release.Signature == null || distribution.PublicKey.Length == 0) throw new IOException("This release has no signed AppImage update.");
        var bytes = await FetchAsync(release.Manifest, 16384, token);
        var signature = await FetchAsync(release.Signature, 4096, token);
        using var rsa = RSA.Create(); rsa.ImportFromPem(distribution.PublicKey);
        if (!rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new IOException("Update signature verification failed.");
        var update = JsonSerializer.Deserialize<SignedUpdate>(bytes) ?? throw new IOException("Invalid update manifest.");
        if (ParseVersion(update.Version) != release.Version || update.Channel != distribution.Channel || update.Size is < 16 or > 2147483648 || !Regex.IsMatch(update.Sha256, "^[a-fA-F0-9]{64}$")) throw new IOException("Update manifest does not match the selected release.");
        distribution.ValidateUrl(update.Url); return update;
    }
    public async Task<string> StageAsync(SignedUpdate update, string currentImage, Action<int> progress, CancellationToken token)
    {
        ValidateTarget(currentImage);
        distribution.ValidateUrl(update.Url);
        var stage = currentImage + ".download-" + Guid.NewGuid().ToString("N");
        try
        {
            using var response = await http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != update.Size) throw new IOException("Update download length mismatch.");
            await using (var output = new FileStream(stage, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }))
            {
                using var input = await response.Content.ReadAsStreamAsync(token); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[131072]; long total = 0; int read; progress(0);
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += read; if (total > update.Size) throw new IOException("Update download exceeds the declared size.");
                    hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token); progress((int)(total * 100 / update.Size));
                }
                if (total != update.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update download checksum mismatch.");
                await output.FlushAsync(token); output.Flush(true);
            }
            var header = new byte[11]; using (var file = File.OpenRead(stage)) file.ReadExactly(header);
            if (!header.AsSpan(0,4).SequenceEqual(new byte[] {127,69,76,70}) || header[8] != 65 || header[9] != 73 || header[10] != 2) throw new IOException("Update is not a type-2 AppImage.");
            File.SetUnixFileMode(stage, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return stage;
        }
        catch { if (File.Exists(stage)) File.Delete(stage); throw; }
    }
    public static void ValidateTarget(string image)
    {
        if (!Path.IsPathFullyQualified(image) || !File.Exists(image) || new FileInfo(image).LinkTarget != null) throw new IOException("The AppImage must be a regular file at an absolute, writable location.");
    }
}
