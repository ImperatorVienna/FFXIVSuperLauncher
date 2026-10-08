using System.IO.Compression;
using Newtonsoft.Json.Linq;
using XIVLauncher.Common.Http;
namespace XIVLauncher.Linux;

public sealed record PluginUpdate(OfflinePlugin Plugin, Version Version, Uri Repository, Uri Download, bool Testing, string ManifestSnapshot);
public sealed record PluginUpdatePlan(string Root, string Region, string ConfigSnapshot, IReadOnlyList<PluginUpdate> Updates, IReadOnlyList<string> Problems);

// No plugin assemblies are loaded. Repository identity is compared before selecting by InternalName.
public sealed class PluginUpdates(HttpClient client)
{
    internal static IEnumerable<JToken> Array(JToken? token) => token switch { JArray a => a, JObject o when o["$values"] is JArray a => a, _ => [] };
    internal static Uri Url(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Invalid plugin repository or download URL.");
        return uri;
    }
    private static string ConfigPath(string root, string region) => Path.Combine(root, region, "dalamud", "dalamudConfig.json");
    private static string ReadConfig(string root, string region)
    {
        var path = ConfigPath(root, region); SafePath(root, path);
        return File.Exists(path) ? File.ReadAllText(path) : "{}";
    }
    public async Task<PluginUpdatePlan> CheckAsync(string root, string region, Version dalamudVersion, CancellationToken token)
    {
        root = Path.GetFullPath(root);
        var installed = OfflinePluginManager.Read(root, region);
        var configText = ReadConfig(root, region); var config = JObject.Parse(configText);
        var official = Url(region == "ffxiv" ? "https://kamori.goats.dev/Plugin/PluginMaster" : config.Value<string>("MainRepoUrl") ?? (region == "ffxiv_cn"
            ? "https://gh.atmoomen.top/raw.githubusercontent.com/Dalamud-DailyRoutines/PluginDistD17/main/pluginmaster.json"
            : "https://raw.githubusercontent.com/yanmucorp/PluginDistD17/refs/heads/main/pluginmaster.json"));
        var repos = new Dictionary<Uri, JArray?>();
        var enabled = new HashSet<Uri> { official };
        var problems = new List<string>(installed.Problems); var updates = new List<PluginUpdate>();
        foreach (var repo in Array(config["ThirdRepoList"]).OfType<JObject>())
            if (repo.Value<bool?>("IsEnabled") == true)
                try { enabled.Add(Url(repo.Value<string>("Url") ?? "")); } catch (IOException) { problems.Add("A configured repository URL is invalid."); }
        foreach (var plugin in installed.Plugins)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (plugin.ScheduledForDeletion) continue;
                var localText = File.ReadAllText(plugin.ManifestPath); var local = JObject.Parse(localText);
                var source = local.Value<string>("InstalledFromUrl");
                if (string.IsNullOrWhiteSpace(source)) throw new IOException("Installation source is missing; no repository fallback was used.");
                var repo = source == "OFFICIAL" ? official : Url(source);
                if (!enabled.Contains(repo)) throw new IOException("The original repository is absent or disabled.");
                if (!repos.TryGetValue(repo, out var catalog))
                {
                    repos[repo] = null; // A failed repository is not retried for every plugin.
                    var text = await client.GetStringAsync(repo, token);
                    catalog = JArray.Parse(text); repos[repo] = catalog;
                }
                if (catalog == null) throw new IOException("The original repository is unavailable.");
                var matches = catalog.OfType<JObject>().Where(x => x.Value<string>("InternalName") == plugin.InternalName).ToArray();
                if (matches.Length != 1) throw new IOException("The original repository has no unique matching plugin.");
                var remote = matches[0];
                var testing = local.Value<bool?>("Testing") == true;
                // Keep the installed channel. Never switch stable plugins into testing during an offline update.
                if (testing && (config.Value<bool?>("DoPluginTest") != true || !Array(config["PluginTestingOptIns"]).Any(x => x.Value<string>("InternalName") == plugin.InternalName)))
                    throw new IOException("Testing updates are not enabled for this installed testing plugin.");
                if (!testing && remote.Value<bool?>("IsTestingExclusive") == true) continue;
                if (!Version.TryParse(remote.Value<string>(testing ? "TestingAssemblyVersion" : "AssemblyVersion"), out var version)) throw new IOException("Repository plugin version is invalid.");
                if (version <= Version.Parse(plugin.Version)) continue;
                var api = remote.Value<int?>(testing ? "TestingDalamudApiLevel" : "DalamudApiLevel");
                if (api != dalamudVersion.Major) throw new IOException("The update does not match the installed Dalamud API level.");
                if (remote.Value<string>("MinimumDalamudVersion") is { Length: > 0 } minimum && (!Version.TryParse(minimum, out var min) || min > dalamudVersion))
                    throw new IOException("The update requires a newer Dalamud version.");
                var download = Url(remote.Value<string>(testing ? "DownloadLinkTesting" : "DownloadLinkInstall") ?? "");
                updates.Add(new(plugin, version, repo, download, testing, localText));
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException or Newtonsoft.Json.JsonException or ArgumentException or OperationCanceledException && !token.IsCancellationRequested)
            { problems.Add(plugin.InternalName + ": " + (ex is HttpRequestException or OperationCanceledException ? "The original repository request failed or timed out." : ex.Message)); }
        }
        return new(root, region, configText, updates, problems);
    }
    public async Task InstallAsync(PluginUpdatePlan plan, PluginUpdate update, Action ensureIdle, CancellationToken token)
    {
        if (!plan.Updates.Contains(update)) throw new IOException("Plugin update does not belong to this plan.");
        var root = plan.Root; var stage = Path.Combine(root, ".plugin-update-" + Guid.NewGuid().ToString("N"));
        SafePath(root, stage); Directory.CreateDirectory(stage);
        try
        {
            var zip = Path.Combine(stage, "download.zip");
            await UpdateDownload.FileAsync(client, update.Download.AbsoluteUri, zip, token);
            var extracted = Path.Combine(stage, "files"); Directory.CreateDirectory(extracted);
            using (var archive = ZipFile.OpenRead(zip))
            {
                if (archive.Entries.Count > 20000 || archive.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024) throw new IOException("Plugin archive is too large.");
                foreach (var entry in archive.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (entry.FullName.Contains('\\') || entry.FullName.Contains(':') || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000) throw new IOException("Plugin archive contains an unsafe path.");
                    var path = Path.GetFullPath(Path.Combine(extracted, entry.FullName)); SafePath(extracted, path);
                    if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path);
                }
            }
            var manifestPath = Path.Combine(extracted, update.Plugin.InternalName + ".json");
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (manifest.Value<string>("InternalName") != update.Plugin.InternalName || !Version.TryParse(manifest.Value<string>("AssemblyVersion"), out var version) || version != update.Version
                || !File.Exists(Path.Combine(extracted, update.Plugin.InternalName + ".dll"))) throw new IOException("Downloaded plugin identity or version does not match its repository.");
            var old = JObject.Parse(update.ManifestSnapshot);
            foreach (var key in new[] { "WorkingPluginId", "Disabled", "ScheduledForDeletion", "InstalledFromUrl" })
                if (old[key] != null) manifest[key] = old[key]!.DeepClone();
                else manifest.Remove(key);
            manifest["Testing"] = update.Testing;
            File.WriteAllText(manifestPath, manifest.ToString());
            File.Delete(Path.Combine(extracted, ".disabled")); File.Delete(Path.Combine(extracted, ".testing"));
            if (File.Exists(Path.Combine(Path.GetDirectoryName(update.Plugin.ManifestPath)!, ".disabled"))) File.WriteAllText(Path.Combine(extracted, ".disabled"), "");
            var target = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(update.Plugin.ManifestPath))!, update.Version.ToString());
            using var gate = new FileStream(Path.Combine(root, ".plugin-sync.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            token.ThrowIfCancellationRequested(); ensureIdle();
            SafePath(root, target); SafePath(root, update.Plugin.ManifestPath);
            if (ReadConfig(root, plan.Region) != plan.ConfigSnapshot || File.ReadAllText(update.Plugin.ManifestPath) != update.ManifestSnapshot) throw new IOException("Plugin settings changed; check updates again.");
            var current = OfflinePluginManager.Read(root, plan.Region).Plugins.SingleOrDefault(x => x.InternalName == update.Plugin.InternalName);
            if (current?.ManifestPath != update.Plugin.ManifestPath || current.WorkingId != update.Plugin.WorkingId) throw new IOException("Installed plugin changed; check updates again.");
            if (Directory.Exists(target)) throw new IOException("The target plugin version already exists; no files were overwritten.");
            Directory.Move(extracted, target); // Same filesystem; old version/config remain intact on failure.
        }
        finally { Directory.Delete(stage, true); }
    }
    private static void SafePath(string root, string path)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); path = Path.GetFullPath(path);
        if (path != root && !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new IOException("Plugin path escapes its data directory.");
        for (var p = path; p != null; p = Path.GetDirectoryName(p))
        {
            if (new FileInfo(p).LinkTarget != null || new DirectoryInfo(p).LinkTarget != null) throw new IOException("Plugin path contains a symbolic link.");
            if (p == root) break;
        }
    }
}
