using System.Text.Json;
using System.Text.RegularExpressions;
using XIVLauncher.Common.Constant;
namespace XIVLauncher.Linux;

public sealed record DalamudAsset(string File, string Url, string? Sha1);
public sealed record DalamudDistribution(string Version, string Archive, string? Hashes, string RuntimeVersion,
    int AssetVersion, IReadOnlyList<DalamudAsset> Assets, string? AssetPackage, bool Soil, bool Zip = false, string? SupportedGameVersion = null);
public interface IDalamudSource
{
    Task<DalamudDistribution> ResolveAsync(HttpClient client, CancellationToken token);
}
public static class DalamudSources
{
    public static IDalamudSource For(string region) => region switch
    { "ffxiv" => new GlobalSource(), "ffxiv_cn" => new SoilSource(), "ffxiv_tc" => new TaiwanSource(), _ => throw new NotSupportedException("此区服尚未接入 Dalamud 更新源。") };
    internal static void ValidateVersion(string value)
    { if (!Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9._-]*$")) throw new IOException("Dalamud 版本标识无效。"); }
    private sealed class GlobalSource : IDalamudSource
    {
        public async Task<DalamudDistribution> ResolveAsync(HttpClient client, CancellationToken token)
        {
            using var release = JsonDocument.Parse(await client.GetStringAsync("https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=release", token));
            using var manifest = JsonDocument.Parse(await client.GetStringAsync("https://kamori.goats.dev/Dalamud/Asset/Meta", token));
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var info = JsonSerializer.Deserialize<GlobalRelease>(release.RootElement.GetRawText(), options)!;
            var assets = JsonSerializer.Deserialize<GlobalAssets>(manifest.RootElement.GetRawText(), options)!;
            ValidateVersion(info.AssemblyVersion); ValidateVersion(info.RuntimeVersion);
            return new(info.AssemblyVersion, info.DownloadUrl, null, info.RuntimeVersion, assets.Version,
                assets.Assets.Select(a => new DalamudAsset(a.FileName, a.Url, a.Hash)).ToArray(), null, false, true, info.SupportedGameVer);
        }
        private sealed record GlobalRelease(string AssemblyVersion, string RuntimeVersion, string DownloadUrl, string? SupportedGameVer);
        private sealed record GlobalAssets(int Version, GlobalAsset[] Assets);
        private sealed record GlobalAsset(string FileName, string Url, string Hash);
    }
    private sealed class SoilSource : IDalamudSource
    {
        public async Task<DalamudDistribution> ResolveAsync(HttpClient client, CancellationToken token)
        {
            var version = (await client.GetStringAsync(Links.DALAMUD_DISTRIBUTE_R2_VERSION_URL, token)).Trim(); ValidateVersion(version);
            var runtime = (await client.GetStringAsync(Links.DALAMUD_RUNTIME_INFO_URL, token)).Trim(); ValidateVersion(runtime);
            var assetVersion = int.Parse((await client.GetStringAsync(Links.DALAMUD_ASSET_DISTRIBUTE_R2_VERSION_URL, token)).Trim());
            var assetBase = $"{Links.DALAMUD_ASSET_DISTRIBUTE_R2_BASE_URL}/{assetVersion}";
            using var manifest = JsonDocument.Parse(await client.GetStringAsync(assetBase + "/assetCN.json", token));
            var assets = manifest.RootElement.GetProperty("Assets").EnumerateArray().Select(a =>
            {
                var file = a.GetProperty("FileName").GetString()!;
                return new DalamudAsset(file, assetBase + "/files/" + file, a.GetProperty("Hash").GetString());
            }).ToArray();
            var release = Links.DALAMUD_DISTRIBUTE_R2_BASE_URL + "/" + version;
            return new(version, release + "/latest.7z", release + "/hashes.json", runtime, assetVersion, assets, null, true);
        }
    }
    private sealed class TaiwanSource : IDalamudSource
    {
        private static readonly string[] Manifests = ["https://api.github.com/repos/yanmucorp/Dalamud/releases/latest",
            "https://cdn.jsdelivr.net/gh/cycleapple/XIVTCLauncher@main/cdn/dalamud/latest-release.json",
            "https://fastly.jsdelivr.net/gh/cycleapple/XIVTCLauncher@main/cdn/dalamud/latest-release.json"];
        public async Task<DalamudDistribution> ResolveAsync(HttpClient client, CancellationToken token)
        {
            JsonDocument? release = null;
            foreach (var url in Manifests)
            {
                try { release = JsonDocument.Parse(await client.GetStringAsync(url, token)); break; }
                catch (Exception ex) when (ex is HttpRequestException or JsonException) { }
            }
            using var manifest = release ?? throw new IOException("无法获取 Dalamud 发行清单。");
            var tag = manifest.RootElement.GetProperty("tag_name").GetString()!; ValidateVersion(tag);
            string Asset(string name) => manifest.RootElement.GetProperty("assets").EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("browser_download_url").GetString()!;
            using var info = JsonDocument.Parse(await client.GetStringAsync("https://raw.githubusercontent.com/yanmucorp/DalamudAssets/master/assetCN.json", token));
            // xl_tw does not enforce asset hashes; the published font hash can lag its URL.
            // Preserve upstream asset policy. Executable/DLL hashes remain mandatory in the shared installer.
            var files = info.RootElement.GetProperty("Assets").EnumerateArray().Select(a => new DalamudAsset(a.GetProperty("FileName").GetString()!, a.GetProperty("Url").GetString()!, null)).ToArray();
            return new(tag, Asset("latest.7z"), Asset("hashes.json"), "9.0.11", info.RootElement.GetProperty("Version").GetInt32(), files,
                info.RootElement.TryGetProperty("PackageUrl", out var package) ? package.GetString() : null, false);
        }
    }
}
