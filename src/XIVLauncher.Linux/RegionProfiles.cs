namespace XIVLauncher.Linux;

// A broken region file must neither block unrelated regions nor be overwritten
// with defaults. Direct use of that region still fails explicitly.
public sealed class RegionProfiles : IEnumerable<KeyValuePair<string,RegionSettings>>
{
    private readonly Dictionary<string, RegionSettings> profiles = LinuxSettings.Regions.ToDictionary(r => r.Id, _ => new RegionSettings());
    public RegionSettings this[string region]
    {
        get => profiles.TryGetValue(region, out var profile) ? profile : throw new IOException($"Cannot read {region} settings; original file was preserved.");
        internal set => profiles[region] = value;
    }
    public IEnumerable<RegionSettings> Values => profiles.Values;
    public IEnumerator<KeyValuePair<string,RegionSettings>> GetEnumerator() => profiles.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    internal bool TryGetValue(string region, out RegionSettings profile) => profiles.TryGetValue(region, out profile!);
    internal void Unavailable(string region) => profiles.Remove(region);
}
