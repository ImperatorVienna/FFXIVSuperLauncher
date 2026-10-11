using System.Text.RegularExpressions;
namespace XIVLauncher.Linux;
internal sealed record CleanupResult(int Deleted, long Bytes, int Skipped, string Kind);

internal static class LauncherCleanupService
{
    internal static CleanupResult Clean(string root, string kind, string image, CancellationToken token)
    {
        if (kind is not ("downloads" or "logs")) throw new ArgumentException("Unknown cleanup kind.");
        using var cacheLease = kind == "downloads" ? DownloadCacheLock.Acquire(root) : null;
        var deleted = 0; long bytes = 0; var skipped = 0;
        if (kind == "logs")
            Scan(Path.Combine(root, "logs"), name => name.StartsWith("launcher-", StringComparison.Ordinal) && name.EndsWith(".log", StringComparison.Ordinal));
        else
        {
            Scan(root, name => Regex.IsMatch(name, @"^\.plugin-update-[a-f0-9]{32}$"));
            foreach (var region in new[] { "ffxiv_cn", "ffxiv", "ffxiv_tc" })
            {
                var directory = Path.Combine(root, region);
                if (new DirectoryInfo(directory).LinkTarget != null) { skipped++; continue; }
                Remove(Path.Combine(directory, "patches"));
                Scan(directory, name => Regex.IsMatch(name, @"^runtime\.download-[a-f0-9]{32}$"));
                Scan(Path.Combine(directory, "addon"), Staging);
                if (new DirectoryInfo(Path.Combine(directory, "addon")).LinkTarget == null)
                    Scan(Path.Combine(directory, "addon", "Hooks"), Staging);
                Scan(Path.Combine(directory, "assets"), Staging);
            }
            if (Path.IsPathFullyQualified(image))
                Scan(Path.GetDirectoryName(image)!, name => name.StartsWith(Path.GetFileName(image) + ".download-", StringComparison.Ordinal) &&
                    Guid.TryParseExact(name[(Path.GetFileName(image).Length + 10)..], "N", out _));
        }
        return new(deleted, bytes, skipped, kind);

        static bool Staging(string name) => Regex.IsMatch(name, @"^.+\.[a-f0-9]{32}(\.(zip|7z))?$");
        void Scan(string directory, Func<string, bool> match)
        {
            try
            {
                if (new DirectoryInfo(directory).LinkTarget != null) { skipped++; return; }
                if (!Directory.Exists(directory)) return;
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                    if (match(Path.GetFileName(path))) Remove(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
        }
        void Remove(string path)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null) { skipped++; return; }
                if (Directory.Exists(path))
                {
                    foreach (var child in Directory.EnumerateFileSystemEntries(path)) Remove(child);
                    if (!Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
                }
                else if (File.Exists(path))
                {
                    if (Processes.ProcessFileOwner.IsProtected(path + ".in-use")) { skipped++; return; }
                    if (kind == "logs")
                    {
                        bytes += new SessionDiagnostics(root).ClearFile(path); deleted++; return;
                    }
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    var size = stream.Length;
                    File.Delete(path); deleted++; bytes += size;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
        }
    }
}
