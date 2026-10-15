namespace XIVLauncher.Linux.Platforms;

// UI capabilities describe available implementations, not the machine's label.
// A Windows host will supply its own capabilities when its adapters are ready.
internal sealed record PlatformCapabilities(bool CompatibilityTools)
{
    internal string[] Features => new[]
    {
        "preferences.language", "preferences.region", "preferences.paths", "accounts", "launch", "game-update",
        "official-news", "official-browser", "plugins", "plugin-sync", "dalamud-update", "travel", "setup", "about", "launcher-update"
    }.Concat(CompatibilityTools ? new[] { "compatibility" } : Array.Empty<string>()).ToArray();
    internal void RequireOperation(string method)
    {
        if (!CompatibilityTools && method.StartsWith("compatibility.", StringComparison.Ordinal))
            throw new NotSupportedException("Compatibility tools are not supported by this platform.");
    }
}
