using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using XIVLauncher.Common.Http;
using XIVLauncher.Common.Util;
namespace XIVLauncher.Linux;

// Shared installation pipeline. Region adapters supply only manifests, sources and runtime versions.
public sealed class DalamudUpdateService(HttpClient client, IDalamudSource source)
{
    public async Task<RegionDalamudFiles> PrepareAsync(string root, Action<string> progress, CancellationToken token)
    {
        var info = await source.ResolveAsync(client, token);
        DalamudSources.ValidateVersion(info.Version); DalamudSources.ValidateVersion(info.RuntimeVersion);
        var addon = Path.Combine(root, info.Soil ? "addon/Hooks" : "addon", info.Version);
        var runtime = Path.Combine(root, "runtime");
        progress("检查 Dalamud：" + info.Version);
        var hashes = info.Hashes == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(await client.GetStringAsync(info.Hashes, token));
        if (info.Hashes != null && (hashes == null || hashes.Count == 0)) throw new IOException("Dalamud hash manifest is empty.");
        bool Valid(string path)
        {
            if (!File.Exists(Path.Combine(path, "Dalamud.Injector.exe"))) return false;
            var localHashes = hashes;
            if (localHashes == null)
            {
                var manifest = Path.Combine(path, "hashes.json");
                if (!File.Exists(manifest)) return false;
                localHashes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifest));
            }
            return localHashes is { Count: > 0 } && localHashes.All(h =>
            {
                token.ThrowIfCancellationRequested(); var file = SafeChild(path, h.Key); if (!File.Exists(file)) return false;
                using var stream = File.OpenRead(file); return Convert.ToHexString(MD5.HashData(stream)).Equals(h.Value, StringComparison.OrdinalIgnoreCase);
            });
        }
        if (!await Task.Run(() => Valid(addon), token))
        {
            var staging = addon + "." + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(staging);
            var archive = staging + (info.Zip ? ".zip" : ".7z");
            try
            {
                progress("下载 Dalamud"); await UpdateDownload.FileAsync(client, info.Archive, archive, token);
                await Task.Run(() => { if (info.Zip) ZipFile.ExtractToDirectory(archive, staging); else PlatformHelpers.Unzip7ZAsset(archive, staging); }, token);
                if (!await Task.Run(() => Valid(staging), token)) throw new IOException("Dalamud 程序校验失败。");
                ReplaceDirectory(staging, addon);
            }
            finally { if (File.Exists(archive)) File.Delete(archive); if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
        await XIVLauncher.Common.Runtime.DotNetRuntimeManager.EnsureRuntimeAsync(new DirectoryInfo(runtime), info.RuntimeVersion, "win-x64", ".NET Runtime",
            progress, cancellationToken: token, packageSource: RuntimeSourceAsync);
        token.ThrowIfCancellationRequested();
        var assetRoot = Path.Combine(root, "assets", info.AssetVersion.ToString());
        bool ValidAsset(string path, DalamudAsset asset)
        {
            var file = SafeChild(path, asset.File); if (!File.Exists(file)) return false;
            if (string.IsNullOrWhiteSpace(asset.Sha1)) return true;
            using var stream = File.OpenRead(file); return Convert.ToHexString(SHA1.HashData(stream)).Equals(asset.Sha1, StringComparison.OrdinalIgnoreCase);
        }
        if (!await Task.Run(() => info.Assets.All(a => ValidAsset(assetRoot, a)), token) || !Directory.Exists(assetRoot))
        {
            var staging = assetRoot + "." + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(staging);
            try
            {
                progress("更新 Dalamud 资源…");
                if (!string.IsNullOrWhiteSpace(info.AssetPackage))
                {
                    var zip = staging + ".zip";
                    try { await UpdateDownload.FileAsync(client, info.AssetPackage, zip, token); ZipFile.ExtractToDirectory(zip, staging); }
                    finally { if (File.Exists(zip)) File.Delete(zip); }
                }
                else foreach (var asset in info.Assets)
                {
                    token.ThrowIfCancellationRequested();
                    var file = SafeChild(staging, asset.File); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    if (ValidAsset(assetRoot, asset)) File.Copy(SafeChild(assetRoot, asset.File), file);
                    else await UpdateDownload.FileAsync(client, asset.Url, file, token);
                }
                if (!info.Assets.All(a => ValidAsset(staging, a))) throw new IOException("Dalamud 资源校验失败。");
                ReplaceDirectory(staging, assetRoot);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
        LocalDalamud.Record(root, info, assetRoot);
        return new(new FileInfo(Path.Combine(addon, "Dalamud.Injector.exe")), new DirectoryInfo(runtime), new DirectoryInfo(assetRoot), 4, info.Soil, info.SupportedGameVersion);
    }
    private async Task<string> RuntimeSourceAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await client.GetAsync("https://api.nuget.org/v3/index.json", timeout.Token);
            if (response.IsSuccessStatusCode) return "https://api.nuget.org/v3-flatcontainer";
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { token.ThrowIfCancellationRequested(); }
        return "https://repo.huaweicloud.com/artifactory/api/nuget/v3/nuget-remote";
    }
    private static void ReplaceDirectory(string staging, string target)
    {
        var backup = target + ".old-" + Guid.NewGuid().ToString("N");
        if (Directory.Exists(target)) Directory.Move(target, backup);
        try { Directory.Move(staging, target); }
        catch { if (Directory.Exists(backup)) Directory.Move(backup, target); throw; }
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
    }
    internal static string SafeChild(string root, string relative)
    {
        root = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', '/')));
        if (!path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal)) throw new IOException("更新文件路径超出目标目录。");
        return path;
    }
}
