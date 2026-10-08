using System.Security.Cryptography;
namespace XIVLauncher.Linux.Taiwan;

// Verified ffxiv_tc entrypoint adaptation; upstream installation is never modified.
internal static class TaiwanEntrypoint
{
    internal const string OriginalHash = "FEA73493A4517D81057740A5812305729A09BC52E34B7E888A8E740A832BB820";
    internal const string PatchedHash = "E03CA94E7A0F94CDF1068F9B2BFC33FB9F9E23BC98099F806CDBD0D2B883A381";
    internal static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static async Task<FileInfo> PrepareAsync(FileInfo injector, string regionRoot, string patchPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var source = Path.Combine(injector.DirectoryName!, "Dalamud.Injector.dll");
        if (Hash(source) != OriginalHash || Hash(patchPath) != PatchedHash)
            throw new IOException("The ffxiv_tc entrypoint adapter does not support this injector build. Disable Dalamud to launch, or install a compatible launcher update. No installed files were changed.");
        var parent = Path.Combine(regionRoot, "injection-cache", "entrypoint");
        Directory.CreateDirectory(parent);
        // Key the cache by all addon contents, not just the injector version.
        var fingerprint = await Task.Run(() => string.Join("\n", Directory.EnumerateFiles(injector.DirectoryName!, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => { token.ThrowIfCancellationRequested(); return Path.GetRelativePath(injector.DirectoryName!, path) + ":" + Hash(path); })), token);
        var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint)));
        var final = Path.Combine(parent, key);
        if (File.Exists(Path.Combine(final, "complete")) && CacheMatches(final, fingerprint))
            return new FileInfo(Path.Combine(final, injector.Name));
        var target = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        try
        {
            await Task.Run(() => Copy(injector.DirectoryName!, target, token), token);
            File.Copy(patchPath, Path.Combine(target, "Dalamud.Injector.dll"), true);
            File.WriteAllText(Path.Combine(target, "complete"), key);
            if (Directory.Exists(final)) Directory.Delete(final, true);
            Directory.Move(target, final);
            return new FileInfo(Path.Combine(final, injector.Name));
        }
        catch { if (Directory.Exists(target)) Directory.Delete(target, true); throw; }
    }
    private static bool CacheMatches(string directory, string fingerprint)
    {
        try
        {
            foreach (var line in fingerprint.Split('\n'))
            {
                var separator = line.LastIndexOf(':');
                var relative = line[..separator];
                var expected = relative == "Dalamud.Injector.dll" ? PatchedHash : line[(separator + 1)..];
                var file = Path.Combine(directory, relative);
                if (!File.Exists(file) || Hash(file) != expected) return false;
            }
            return true;
        }
        catch (IOException) { return false; }
    }
    private static void Copy(string source, string destination, CancellationToken token)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            token.ThrowIfCancellationRequested();
            if (entry.LinkTarget != null) throw new IOException("The injector directory must not contain symbolic links.");
            if (entry is DirectoryInfo) Copy(entry.FullName, Path.Combine(destination, entry.Name), token);
            else File.Copy(entry.FullName, Path.Combine(destination, entry.Name));
        }
    }
}
