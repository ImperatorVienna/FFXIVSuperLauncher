using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XIVLauncher.Linux;

public sealed record OfflinePlugin(string InternalName, string Name, string Version, string ManifestPath,
    Guid WorkingId, bool? Enabled, bool ScheduledForDeletion);
public sealed record OfflinePluginList(IReadOnlyList<OfflinePlugin> Plugins, IReadOnlyList<string> Problems, string Snapshot);

/// <summary>Edits Dalamud ProfileModelV1 data without instantiating any types from JSON or loading plugin DLLs.</summary>
public static class OfflinePluginManager
{
    private const string ProfileType = "Dalamud.Plugin.Internal.Profiles.ProfileModelV1, Dalamud";
    public static OfflinePluginList Read(string root, string region)
    {
        var folder = RegionPath(root, region);
        var config = ReadConfig(folder);
        var profiles = Profiles(config).ToArray();
        var plugins = new List<OfflinePlugin>(); var problems = new List<string>();
        var fingerprint = new System.Text.StringBuilder(config.ToString(Formatting.None));
        var installed = Path.Combine(folder, "installedPlugins");
        SafePath(folder, installed);
        if (!Directory.Exists(installed)) return new(plugins, problems, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint.ToString()))));
        foreach (var pluginDir in Directory.EnumerateDirectories(installed).Order())
        {
            try
            {
                SafePath(folder, pluginDir);
                var latest = Directory.EnumerateDirectories(pluginDir)
                    .Select(p => (Path: p, Version: Version.TryParse(Path.GetFileName(p), out var v) ? v : null))
                    .Where(p => p.Version != null).OrderByDescending(p => p.Version).FirstOrDefault();
                if (latest.Path == null) continue;
                var name = Path.GetFileName(pluginDir);
                var manifestPath = Path.Combine(latest.Path, name + ".json");
                SafePath(folder, manifestPath); SafePath(folder, Path.Combine(latest.Path, name + ".dll"));
                if (!File.Exists(Path.Combine(latest.Path, name + ".dll"))) throw new IOException("缺少插件 DLL。");
                var manifestText = File.ReadAllText(manifestPath);
                fingerprint.Append(manifestPath).Append(manifestText).Append(File.Exists(Path.Combine(latest.Path, ".disabled")));
                var manifest = JObject.Parse(manifestText);
                if ((string?)manifest["InternalName"] != name) throw new IOException("插件清单名称与目录不一致。");
                var id = (Guid?)manifest["WorkingPluginId"] ?? Guid.Empty;
                var entries = profiles.SelectMany(p => Entries(p.Model).OfType<JObject>().Where(e => Matches(e, name, id)).Select(e => (p, e))).ToArray();
                bool? enabled;
                if (entries.Length == 0) enabled = manifest.Value<bool?>("Disabled") != true && !File.Exists(Path.Combine(latest.Path, ".disabled"));
                else
                {
                    var wanted = entries.Where(x => x.e.Value<bool?>("IsEnabled") == true).ToArray();
                    enabled = wanted.Any(x => x.p.Default && x.p.Model.Value<bool?>("e4c") != true ||
                        !x.p.Default && StartsEnabled(x.p.Model) && x.p.Model.Value<bool?>("e4c") != true)
                        ? true : wanted.Length != 0 ? null : false;
                }
                plugins.Add(new(name, (string?)manifest["Name"] ?? name, latest.Version!.ToString(), manifestPath, id,
                    enabled, manifest.Value<bool?>("ScheduledForDeletion") == true));
            }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
            { problems.Add(Path.GetFileName(pluginDir) + ": " + ex.Message); }
        }
        return new(plugins, problems, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint.ToString()))));
    }

    public static string ApplySelection(string root, string region, OfflinePluginList displayed, IReadOnlyDictionary<string, bool> selection)
    {
        // Apply the explicit checklist against freshly read configuration, not a cached difference.
        // Preserve settings changed in game; refuse a list whose installed plugin identities changed.
        var current = Read(root, region);
        foreach (var name in selection.Keys)
        {
            var old = displayed.Plugins.SingleOrDefault(p => p.InternalName == name);
            var now = current.Plugins.SingleOrDefault(p => p.InternalName == name);
            if (old == null || now == null || old.WorkingId != now.WorkingId || old.ManifestPath != now.ManifestPath)
                throw new IOException("已安装插件发生变化，请点击放弃修改重新读取列表后重试。");
        }
        var changes = selection.Where(pair => current.Plugins.Single(p => p.InternalName == pair.Key).Enabled != pair.Value)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return Apply(root, region, changes, current.Snapshot);
    }

    public static string Apply(string root, string region, IReadOnlyDictionary<string, bool> changes, string expectedSnapshot)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var folder = RegionPath(root, region);
        using var gate = new FileStream(Path.Combine(root, ".plugin-sync.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var current = Read(root, region);
        if (current.Snapshot != expectedSnapshot) throw new IOException("插件或 Dalamud 设置已变化，请点击放弃修改重新读取列表后重试。");
        if (changes.Count == 0) return "";
        var path = Path.Combine(folder, "dalamudConfig.json");
        SafePath(folder, path);
        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        var config = original == null ? new JObject() : JObject.Parse(original);
        var profiles = Profiles(config).ToList();
        if (profiles.All(p => !p.Default))
        {
            var profile = new JObject { ["$type"] = ProfileType, ["n"] = "DEFAULT", ["e"] = true, ["Plugins"] = new JArray() };
            config["DefaultProfile"] = profile; profiles.Insert(0, (profile, true));
        }
        var defaultProfile = profiles.Single(p => p.Default).Model;
        foreach (var (internalName, enabled) in changes)
        {
            if (enabled && defaultProfile.Value<bool?>("e4c") == true)
                throw new IOException("默认插件集合带有角色限制，请先在游戏内调整该规则。");
            var plugin = current.Plugins.SingleOrDefault(p => p.InternalName == internalName)
                ?? throw new IOException("插件已变化或无法读取，请重新读取列表。");
            if (plugin.ScheduledForDeletion) throw new IOException("此插件已计划删除，请在游戏内重新安装。");
            // Disable this plugin in every collection, preserving policies and unrelated entries.
            if (!enabled)
                foreach (var profile in profiles)
                    foreach (var entry in Entries(profile.Model).OfType<JObject>().Where(e => Matches(e, internalName, plugin.WorkingId))) entry["IsEnabled"] = false;
            // An explicit enable works even when a saved collection is inactive or character-specific.
            var entries = Entries(defaultProfile);
            var own = entries.OfType<JObject>().FirstOrDefault(e => Matches(e, internalName, plugin.WorkingId));
            if (own == null)
            {
                own = new JObject { ["InternalName"] = internalName, ["WorkingPluginId"] = plugin.WorkingId.ToString() };
                entries.Add(own);
            }
            own["IsEnabled"] = enabled;
        }
        var backup = Path.Combine(root, "plugin-state-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"), region);
        SafePath(Path.GetFullPath(root), backup);
        Directory.CreateDirectory(backup, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (original != null) WritePrivate(Path.Combine(backup, "dalamudConfig.json"), original);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            WritePrivate(temp, config.ToString(Formatting.Indented));
            if ((File.Exists(path) ? File.ReadAllText(path) : null) != original)
                throw new IOException("Dalamud 设置已被其他程序修改，请重新读取列表后重试。");
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return backup;
    }

    private static bool StartsEnabled(JObject profile) => profile["p"]?.ToString() switch
    {
        "1" or "AlwaysEnable" => true,
        "2" or "AlwaysDisable" => false,
        _ => profile.Value<bool?>("e") == true
    };
    private static bool Matches(JObject entry, string name, Guid id)
    {
        var entryId = (Guid?)entry["WorkingPluginId"] ?? Guid.Empty;
        return id != Guid.Empty && entryId == id || entryId == Guid.Empty && (string?)entry["InternalName"] == name;
    }
    private static IEnumerable<(JObject Model, bool Default)> Profiles(JObject config)
    {
        if (config["DefaultProfile"] is JObject d) { ValidateProfile(d); yield return (d, true); }
        else if (config["DefaultProfile"] is { Type: not JTokenType.Null }) throw new IOException("无法识别默认插件集合。");
        foreach (var p in Array(config["SavedProfiles"]).Cast<JToken>())
        {
            if (p is not JObject profile) throw new IOException("无法识别插件集合。");
            ValidateProfile(profile); yield return (profile, false);
        }
    }
    private static void ValidateProfile(JObject profile)
    {
        if (profile["$type"] is JToken type && (string?)type != ProfileType)
            throw new IOException("不支持此版本的插件集合，未修改设置。");
        _ = Entries(profile);
    }
    private static JArray Entries(JObject profile)
    {
        if (profile["Plugins"] == null || profile["Plugins"]!.Type == JTokenType.Null) profile["Plugins"] = new JArray();
        var array = Array(profile["Plugins"]);
        if (array.Any(e => e is not JObject)) throw new IOException("插件集合条目格式异常。");
        return array;
    }
    private static JArray Array(JToken? value) => value switch
    {
        null => new JArray(),
        { Type: JTokenType.Null } => new JArray(),
        JArray a => a,
        JObject o when o["$values"] is JArray a => a,
        _ => throw new IOException("无法识别插件集合列表格式。")
    };
    private static JObject ReadConfig(string folder)
    {
        var path = Path.Combine(folder, "dalamudConfig.json"); SafePath(folder, path);
        return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : new JObject();
    }
    private static string RegionPath(string root, string region)
    {
        if (!LinuxSettings.Regions.Any(r => r.Id == region)) throw new ArgumentException("未知区服。");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.Combine(root, region, "dalamud"); SafePath(root, path); return path;
    }
    private static void SafePath(string root, string path)
    {
        for (var p = path; ; p = Path.GetDirectoryName(p)!)
        {
            if (new FileInfo(p).LinkTarget != null || new DirectoryInfo(p).LinkTarget != null) throw new IOException("插件路径包含符号链接，未自动跟随。");
            if (p == root) return;
            if (string.IsNullOrEmpty(p)) throw new IOException("插件路径超出区服目录。");
        }
    }
    private static void WritePrivate(string path, string content)
    {
        using var stream = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
        using var writer = new StreamWriter(stream); writer.Write(content);
    }
}
