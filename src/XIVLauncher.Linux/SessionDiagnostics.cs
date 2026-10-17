namespace XIVLauncher.Linux;

/// <summary>Detailed session diagnostics are separate from the short on-screen activity log.</summary>
public sealed class SessionDiagnostics(string root)
{
    private sealed record SessionFile(string Path) { internal readonly object Gate = new(); }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SessionFile> Files = new(Platforms.PlatformServices.Paths.Comparer);
    private readonly SessionFile file = Files.GetOrAdd(Path.GetFullPath(root), directory => new(Path.Combine(directory, "logs", $"launcher-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log")));
    public string FilePath => file.Path;
    public string? WriteError { get; private set; }
    internal long ClearFile(string path)
    {
        lock (file.Gate)
        {
            if (Processes.ProcessFileOwner.IsProtected(path + ".in-use")) throw new IOException("Log owner is active.");
            if (!Platforms.PlatformServices.Paths.Same(Path.GetFullPath(path), file.Path))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!int.TryParse(name[(name.LastIndexOf('-') + 1)..], out var pid) || pid <= 0 ||
                    Processes.ProcessFacts.Read(pid).Presence != Processes.ProcessPresence.Gone)
                    throw new IOException("Other log owner is active or unknown.");
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete);
            var size = stream.Length;
            // Preserve the current log inode; subsequent writes continue normally.
            if (Platforms.PlatformServices.Paths.Same(Path.GetFullPath(path), file.Path)) stream.SetLength(0);
            else File.Delete(path);
            return size;
        }
    }
    public void Write(string message, string region, string level)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        lock (file.Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                using var stream = Platforms.PlatformServices.Storage.OpenPrivateFile(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.WriteLine($"[{DateTimeOffset.Now:O}] [{level}] [{region}] {message}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { WriteError = "Cannot write the diagnostic log. Check disk space and permissions."; }
        }
    }
}

public static class UserLogMessage
{
    public static string Compact(string message)
    {
        var line = EnglishLog.Message(message).Replace("\r", "").Split('\n').FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? "";
        if (line == "Plugin list loaded." || line == "Plugin selection has unapplied changes. Apply or discard changes.") return "";
        foreach (var suffix in new[] { " Previous settings backup:", " Previous data backup:" })
        { var index = line.IndexOf(suffix, StringComparison.Ordinal); if (index >= 0) line = line[..index]; }
        return line.Length <= 260 ? line : line[..257] + "...";
    }
    public static string Failure(Exception error)
    {
        if (error is TimeoutException || error.InnerException is TimeoutException) return "Failed: operation timed out.";
        if (error is OperationCanceledException) return "Operation cancelled.";
        return "Failed: " + Compact(DiagnosticLog.Summary(error));
    }
}
