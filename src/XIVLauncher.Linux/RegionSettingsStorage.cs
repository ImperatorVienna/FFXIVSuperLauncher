using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
namespace XIVLauncher.Linux;

// One in-memory model and account manager, with region-specific persistence
// contracts. Removing properties from the contract also ignores stale foreign
// fields on read, without touching credential files or other regions' settings.
internal static class RegionSettingsStorage
{
    private static readonly HashSet<string> InternationalOnly =
    [nameof(RegionSettings.ClientLanguage), nameof(RegionSettings.SteamAccount), nameof(RegionSettings.FreeTrial),
     nameof(RegionSettings.CdKeyVersion), nameof(RegionSettings.EuShopLanguage),
     nameof(RegionSettings.UseSteamPrefix), nameof(RegionSettings.SteamCompatibilityData)];
    private static readonly HashSet<string> ChinaOnly =
    [nameof(RegionSettings.AreaName), nameof(RegionSettings.DeviceProfiles)];
    private static readonly IReadOnlyDictionary<string, JsonSerializerOptions> Contracts =
        LinuxSettings.Regions.ToDictionary(r => r.Id, r => CreateOptions(r.Id));

    private static JsonSerializerOptions CreateOptions(string region)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type =>
        {
            if (type.Type != typeof(RegionSettings) && type.Type != typeof(SavedAccountSettings)) return;
            foreach (var property in type.Properties.ToArray())
                if ((region != "ffxiv" && InternationalOnly.Contains(property.Name)) ||
                    (region != "ffxiv_cn" && ChinaOnly.Contains(property.Name)) ||
                    (region == "ffxiv_cn" && property.Name == nameof(RegionSettings.AutoOtp)))
                    type.Properties.Remove(property);
        });
        return new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = resolver };
    }
    private static JsonSerializerOptions Options(string region) => Contracts.TryGetValue(region, out var options)
        ? options : throw new ArgumentException("Unknown configuration region.", nameof(region));
    internal static string Serialize(string region, RegionSettings settings) => JsonSerializer.Serialize(settings, Options(region));
    internal static RegionSettings Deserialize(string region, string json) =>
        JsonSerializer.Deserialize<RegionSettings>(json, Options(region)) ?? throw new IOException("Empty region configuration.");
}
