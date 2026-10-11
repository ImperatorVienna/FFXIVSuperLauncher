namespace XIVLauncher.Linux.Processes;

// Layer 3: associate recognized processes with a Wine/Proton environment.
internal static class ProcessSessions
{
    internal static string CanonicalDirectory(string path)
    {
        var full = Path.GetFullPath(path); var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (Directory.Exists(current)) current = new DirectoryInfo(current).ResolveLinkTarget(true)?.FullName ?? current;
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
    internal static bool Matches(string environment, string variable, string expected) => environment.Split('\0').Any(entry =>
    {
        if (!entry.StartsWith(variable + "=", StringComparison.Ordinal)) return false;
        var value = entry[(variable.Length + 1)..];
        if (!Path.IsPathFullyQualified(value)) return false;
        try { return CanonicalDirectory(value) == CanonicalDirectory(expected); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    });
    internal sealed record Inspection(IReadOnlyList<ProcessFact> Matched, IReadOnlyList<ProcessFact> Unassigned)
    {
        internal ProcessPresence Presence => Matched.Count > 0 ? ProcessPresence.Alive : Unassigned.Count > 0 ? ProcessPresence.Unknown : ProcessPresence.Gone;
    }
    internal static ProcessPresence Find(string prefix, bool includeWine, string? data = null) => Inspect(prefix, includeWine, data).Presence;
    internal static Inspection Inspect(string prefix, bool includeWine, string? data = null)
    {
        var matched = new List<ProcessFact>(); var unassigned = new List<ProcessFact>();
        foreach (var fact in ProcessFacts.Snapshot())
        {
            if (fact.Presence == ProcessPresence.Gone || !(includeWine ? ProcessIdentity.IsPrefixOwner(fact.Name) : ProcessIdentity.IsGameProcessName(fact.Name))) continue;
            var environment = ProcessFacts.EnvironmentOf(fact);
            if (environment == null) { unassigned.Add(fact); continue; }
            if (Matches(environment, "WINEPREFIX", prefix) || data != null && Matches(environment, "STEAM_COMPAT_DATA_PATH", data)) matched.Add(fact);
        }
        return new(matched, unassigned);
    }
}
