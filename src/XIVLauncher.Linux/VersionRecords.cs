namespace XIVLauncher.Linux;
public static class VersionRecords
{
    public static string? DalamudAssembly(string root, string region, string? fallbackVersion = null)
    {
        var record = Path.Combine(root, region, "local-dalamud.json");
        var version = fallbackVersion;
        if (File.Exists(record))
        { using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(record)); version = json.RootElement.GetProperty("Version").GetString(); }
        if (string.IsNullOrWhiteSpace(version)) return null;
        DalamudSources.ValidateVersion(version);
        var path = Path.Combine(root, region, region == "ffxiv_cn" ? "addon/Hooks" : "addon", version, "Dalamud.dll");
        return File.Exists(path) ? path : null;
    }
    public static void Refresh(LinuxSettings settings)
    {
        foreach (var region in LinuxSettings.Regions)
        {
            var profile = settings.RegionProfiles[region.Id]; InitializeClient(profile); var previous = profile.DalamudVersion; profile.DalamudVersion = "";
            try
            {
                var dll = DalamudAssembly(settings.Root, region.Id, previous);
                if (dll != null) { _ = System.Reflection.AssemblyName.GetAssemblyName(dll); profile.DalamudVersion = Path.GetFileName(Path.GetDirectoryName(dll))!; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or BadImageFormatException or KeyNotFoundException or InvalidOperationException) { }
        }
    }
    public static void InitializeClient(RegionSettings profile)
    {
        profile.ClientVersion = ""; profile.VersionGamePath = profile.GamePath;
        if (string.IsNullOrWhiteSpace(profile.GamePath)) return;
        var file = Path.Combine(profile.GamePath, "game", "ffxivgame.ver");
        try { if (File.Exists(file)) profile.ClientVersion = File.ReadAllText(file).Trim(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
