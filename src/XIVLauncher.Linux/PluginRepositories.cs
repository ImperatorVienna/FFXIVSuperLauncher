using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace XIVLauncher.Linux;

public sealed record PluginRepositoryEntry(string Url, bool IsEnabled);
public sealed record PluginRepositorySnapshot(string Revision, string Main, bool PresetsEnabled, string[] Presets, PluginRepositoryEntry[] Entries);

public static class PluginRepositories
{
    // Matches Dalamud-DailyRoutines/Dalamud PluginRepository.PresetRepos (2026-10-11).
    internal static readonly string[] ChinaPresets = [
        "https://gh.atmoomen.top/raw.githubusercontent.com/AtmoOmen/DalamudPlugins/main/pluginmaster.json",
        "https://gp.xuolu.com/love.json",
        "https://raw.githubusercontent.com/extrant/DalamudPlugins/main/pluginmaster.json",
        "https://plogon.meowrs.com/cn",
        "https://raw.githubusercontent.com/RedAsteroid/DalamudPlugins/main/pluginmaster.json",
        "https://raw.githubusercontent.com/NiGuangOwO/DalamudPlugins/main/pluginmaster.json"
    ];
    internal static Uri MainRepository(string region, JObject config) => PluginUpdates.Url(region == "ffxiv"
        ? "https://kamori.goats.dev/Plugin/PluginMaster"
        : config.Value<string>("MainRepoUrl") ?? (region == "ffxiv_cn"
            ? "https://gh.atmoomen.top/raw.githubusercontent.com/Dalamud-DailyRoutines/PluginDistD17/main/pluginmaster.json"
            : "https://raw.githubusercontent.com/yanmucorp/PluginDistD17/refs/heads/main/pluginmaster.json"));
    internal static bool PresetsEnabled(string region, JObject config) => region == "ffxiv_cn" && (config.Value<bool?>("AddPresetThirdRepos") ?? true);
    internal static HashSet<Uri> Enabled(string region, JObject config, List<string> problems)
    {
        var enabled = new HashSet<Uri> { MainRepository(region, config) };
        foreach (var entry in PluginUpdates.Array(config["ThirdRepoList"]).OfType<JObject>())
            if (entry.Value<bool?>("IsEnabled") == true)
                try { enabled.Add(PluginUpdates.Url(entry.Value<string>("Url") ?? "")); }
                catch (IOException) { problems.Add("A configured repository URL is invalid."); }
        // Upstream adds presets independently of ThirdRepoList when this switch is on.
        if (PresetsEnabled(region, config)) foreach (var url in ChinaPresets) enabled.Add(PluginUpdates.Url(url));
        return enabled;
    }
    private static string ConfigPath(string root, string region)
    {
        if (!LinuxSettings.Regions.Any(r => r.Id == region)) throw new ArgumentException("Unknown region.");
        var path = Path.Combine(root, region, "dalamud", "dalamudConfig.json"); PluginUpdates.SafePath(root, path); return path;
    }
    private static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : "{}";
    private static string Revision(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static PluginRepositorySnapshot Read(string root, string region)
    {
        var text = ReadText(ConfigPath(root, region)); var config = JObject.Parse(text);
        return Snapshot(region,config,text);
    }
    private static PluginRepositorySnapshot Snapshot(string region,JObject config,string text) => new(Revision(text), MainRepository(region, config).AbsoluteUri, PresetsEnabled(region, config), region == "ffxiv_cn" ? ChinaPresets : [],
            PluginUpdates.Array(config["ThirdRepoList"]).OfType<JObject>().Select(x => new PluginRepositoryEntry(x.Value<string>("Url") ?? "", x.Value<bool?>("IsEnabled") == true)).ToArray());
    public static PluginRepositorySnapshot Save(string root, string region, string revision, PluginRepositoryEntry[] entries, bool presetsEnabled, Action ensureIdle)
    {
        if (entries == null || entries.Length > 100 || region != "ffxiv_cn" && presetsEnabled) throw new ArgumentException("Invalid repository settings.");
        PluginRepositoryEntry[] normalized;
        try { normalized = entries.Select(e => new PluginRepositoryEntry(PluginUpdates.Url((e.Url ?? "").Trim()).AbsoluteUri, e.IsEnabled)).ToArray(); }
        catch (IOException) { throw new CredentialValidationException("Invalid repository URL.", "仓库地址必须是有效的 HTTP 或 HTTPS 地址。"); }
        if (normalized.Select(e => e.Url).Distinct().Count() != normalized.Length) throw new ArgumentException("Duplicate repository URL.");
        var path = ConfigPath(root, region); Directory.CreateDirectory(root);
        var gatePath = Path.Combine(root, ".plugin-sync.lock"); PluginUpdates.SafePath(root, gatePath);
        using var gate = new FileStream(gatePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        ensureIdle(); var text = ReadText(path);
        if (Revision(text) != revision) throw new CredentialValidationException("Repository configuration changed.", "仓库设置已变化，请刷新仓库状态后重试。");
        var config = JObject.Parse(text);
        if (normalized.Any(e => PluginUpdates.Url(e.Url) == MainRepository(region, config))) throw new ArgumentException("The main repository is already included.");
        var previous = PluginUpdates.Array(config["ThirdRepoList"]).OfType<JObject>().ToArray();
        var values = new JArray(normalized.Select(e => {
            var item = (JObject?)previous.FirstOrDefault(x => x.Value<string>("Url") == e.Url)?.DeepClone() ?? new JObject();
            item["Url"] = e.Url; item["IsEnabled"] = e.IsEnabled; return item;
        }));
        if (config["ThirdRepoList"] is JObject wrapper) wrapper["$values"] = values;
        else config["ThirdRepoList"] = values;
        if (region == "ffxiv_cn") config["AddPresetThirdRepos"] = presetsEnabled;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var savedText=config.ToString();
        var result=Snapshot(region,config,savedText);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temp, savedText);
            if (File.Exists(path)) File.Copy(path, path + "." + Guid.NewGuid().ToString("N") + ".bak");
            File.Move(temp, path, true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
        return result;
    }
}
