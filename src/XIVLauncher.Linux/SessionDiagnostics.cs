namespace XIVLauncher.Linux;

/// <summary>Detailed session diagnostics are separate from the short on-screen activity log.</summary>
public sealed class SessionDiagnostics(string root)
{
    private readonly object gate = new();
    public string FilePath { get; } = Path.Combine(root, "logs", $"launcher-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
    public string? WriteError { get; private set; }
    public void Write(string message, string region, string level)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                using var stream = new FileStream(FilePath, new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.Read,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
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
