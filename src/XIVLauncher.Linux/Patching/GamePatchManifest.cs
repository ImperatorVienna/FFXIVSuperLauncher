using System.Text.RegularExpressions;

namespace XIVLauncher.Linux.Patching;

// Shared wire format; callers supply the region's host policy and boot/game mode.
// No login, UI, settings or transport dependency: parsing can fail independently.
// This is the current TSV/SHA-1 format, not a universal vendor protocol. A region
// adapter may replace this parser and still return GamePatch to ZiPatchInstaller.
// If the payload format changes too, that adapter must select another installer;
// never silently interpret a new format as this one or change other regions.
internal static class GamePatchManifest
{
    public static IReadOnlyList<GamePatch> Parse(string text, bool boot, Func<Uri, bool> allowed, string region)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var patches = new List<GamePatch>();
        foreach (var line in text.Split('\n'))
        {
            var value = line.Trim();
            if (value.Length == 0 || value.StartsWith("--") || value.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) || value.StartsWith("X-", StringComparison.OrdinalIgnoreCase)) continue;
            var fields = value.Split('\t');
            if (fields.Length != (boot ? 6 : 9) || !long.TryParse(fields[0], out var length) || length <= 0 ||
                !GamePatchFiles.VersionPattern.IsMatch(fields[4]) ||
                !Uri.TryCreate(fields[^1], UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") || !allowed(url))
                throw new IOException($"{region} patch manifest is invalid.");
            var block = 0; string[] hashes = [];
            if (!boot)
            {
                if (fields[5] != "sha1" || !int.TryParse(fields[6], out block) || block <= 0)
                    throw new IOException($"{region} patch hashes are invalid.");
                hashes = fields[7].Split(',');
                // Avoid overflow when a malformed manifest advertises Int64.MaxValue bytes.
                if (hashes.LongLength != 1 + (length - 1) / block || hashes.Any(h => !Regex.IsMatch(h, "^[a-fA-F0-9]{40}$")))
                    throw new IOException($"{region} patch hashes are invalid.");
            }
            var repository = Regex.Match(url.AbsolutePath, @"/ex([1-5])/");
            patches.Add(new(length, fields[4], repository.Success ? int.Parse(repository.Groups[1].Value) : 0, block, hashes, url));
        }
        if (patches.Count == 0) throw new IOException($"{region} patch manifest contains no recognizable patches.");
        return patches;
    }
}
