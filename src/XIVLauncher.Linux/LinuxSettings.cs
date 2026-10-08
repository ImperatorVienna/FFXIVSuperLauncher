using System.Text.Json;
using System.Text.Json.Serialization;
using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Common.Unix;

namespace XIVLauncher.Linux;

public sealed record RegionInfo(string Id, string Name, bool Available);
public sealed class RegionSettings
{
    public InternationalClientLanguage ClientLanguage { get; set; } = InternationalClientLanguage.English;
    public string GamePath { get; set; } = "";
    public string Account { get; set; } = "";
    public Dictionary<string, SavedAccountSettings> Accounts { get; set; } = new();
    public string AreaName { get; set; } = "";
    public bool RememberPassword { get; set; }
    public bool AutoOtp { get; set; }
    public bool SteamAccount { get; set; }
    public bool FreeTrial { get; set; }
    private string cdKeyVersion = "NA";
    public string CdKeyVersion { get => cdKeyVersion; set => cdKeyVersion = InternationalWeb.NormalizeCdKeyVersion(value); }
    public string EuShopLanguage { get; set; } = "en-gb";
    public bool UseSteamPrefix { get; set; }
    public string SteamCompatibilityData { get; set; } = "";
    public bool EnableDalamud { get; set; }
    public string ClientVersion { get; set; } = "";
    public string VersionGamePath { get; set; } = "";
    public string DalamudVersion { get; set; } = "";
    public Dictionary<string, DeviceProfileSnapshot> DeviceProfiles { get; set; } = new();
}

public sealed class LinuxSettings
{
    public static readonly RegionInfo[] Regions =
    [new("ffxiv_cn", "中国区", true), new("ffxiv_tc", "繁中区", true), new("ffxiv", "国际区", true)];
    public WindowPlacement? WindowPlacement { get; set; }
    public int SchemaVersion { get; set; } = 2;
    public string Language { get; set; } = "en";
    public string SelectedRegion { get; set; } = "ffxiv_cn";
    public bool SetupComplete { get; set; }
    public string ProtonScript { get; set; } = "";
    public string SteamRoot { get; set; } = "";
    public string CompatibilityData { get; set; } = Path.Combine(DataRoot, "compatdata");
    public string RegisteredSteamRoot { get; set; } = "";
    [JsonIgnore] public Dictionary<string, RegionSettings> RegionProfiles { get; } = Regions.ToDictionary(r => r.Id, _ => new RegionSettings());
    [JsonIgnore] public string Root { get; private set; } = DataRoot;
    [JsonIgnore] public RegionSettings Current => RegionProfiles[SelectedRegion];
    [JsonIgnore] public string RegionRoot => Path.Combine(Root, SelectedRegion);
    [JsonIgnore] public string GamePath { get => Current.GamePath; set => Current.GamePath = value; }
    [JsonIgnore] public string Account { get => Current.Account; set => Current.Account = value; }
    [JsonIgnore] public string AreaName { get => Current.AreaName; set => Current.AreaName = value; }
    [JsonIgnore] public bool EnableDalamud { get => Current.EnableDalamud; set => Current.EnableDalamud = value; }
    public static string DataRoot
    {
        get
        {
            var root = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(root) || !Path.IsPathRooted(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share");
            return Path.Combine(root, "xivlauncher-super");
        }
    }

    public void EnsureRegionAvailable()
    {
        if (!Regions.Any(r => r.Id == SelectedRegion && r.Available))
            throw new InvalidOperationException("此区服尚未接入登录与 Dalamud 支持。");
    }

    internal static void EnsureCreated(string root)
    {
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(root, "linux-settings.json");
        if (File.Exists(path)) return;
        var initial = new LinuxSettings { Root = root, CompatibilityData = Path.Combine(root, "compatdata") };
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonSerializer.Serialize(initial, JsonOptions));
            try { File.Move(temp, path, false); }
            catch (IOException) when (File.Exists(path)) { } // Another instance created it first.
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static LinuxSettings Load(string? dataRoot = null)
    {
        var root = dataRoot ?? DataRoot;
        var path = Path.Combine(root, "linux-settings.json");
        if (!File.Exists(path)) return new() { Root = root, CompatibilityData = Path.Combine(root, "compatdata") };
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("SchemaVersion", out var schema) || schema.GetInt32() != 2)
            throw new IOException("此配置版本不受支持，未覆盖原文件。");
        var result = JsonSerializer.Deserialize<LinuxSettings>(json) ?? throw new IOException("配置为空。");
        result.Root = root;
        if (!Regions.Any(r => r.Id == result.SelectedRegion)) throw new IOException("未知区服配置，未覆盖原文件。");
        foreach (var region in Regions)
        {
            var regionFile = Path.Combine(root, region.Id, "settings.json");
            if (File.Exists(regionFile)) result.RegionProfiles[region.Id] = RegionSettingsStorage.Deserialize(region.Id, File.ReadAllText(regionFile));
        }
        foreach (var region in Regions)
        {
            var profile = result.RegionProfiles[region.Id];
            AccountProfiles.Capture(profile);
            // Index legacy plaintext accounts without displaying or copying any secrets.
            foreach (var name in RegionCredentials.SavedAccountNames(root, region.Id))
            {
                if (!profile.Accounts.ContainsKey(name)) profile.Accounts[name] = new() { RememberPassword = true };
            }
        }
        return result;
    }

    public void Save()
    {
        Directory.CreateDirectory(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        foreach (var region in Regions)
        {
            AccountProfiles.Capture(RegionProfiles[region.Id]);
            var dir = Path.Combine(Root, region.Id);
            Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            AtomicWrite(Path.Combine(dir, "settings.json"), RegionSettingsStorage.Serialize(region.Id, RegionProfiles[region.Id]));
        }
        SaveGlobalPreferences();
    }
    internal void SaveGlobalPreferences()
    {
        Directory.CreateDirectory(Root);
        AtomicWrite(Path.Combine(Root, "linux-settings.json"), JsonSerializer.Serialize(this, JsonOptions));
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static void AtomicWrite(string path, string content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }))
            using (var writer = new StreamWriter(stream)) writer.Write(content);
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public DeviceProfileSnapshot GetDeviceProfile(string accountKey)
    {
        if (Current.DeviceProfiles.TryGetValue(accountKey, out var profile)) return profile;
        profile = FakeMachineInfo.CreateSnapshot(); Current.DeviceProfiles.Add(accountKey, profile); Save(); return profile;
    }
    public CompatibilitySettings GetCompatibility(ProtonInstallation proton) => new()
    {
        Executable = proton.Script, DataDirectory = SelectedRegion == "ffxiv" && Current.UseSteamPrefix
            ? SteamPrefixDiscovery.Normalize(Current.SteamCompatibilityData) : Path.GetFullPath(CompatibilityData),
        SteamRoot = proton.SteamRoot, RuntimeEntryPoint = proton.RuntimeEntryPoint
    };
}
