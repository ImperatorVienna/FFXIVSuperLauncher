using System.Diagnostics;
namespace XIVLauncher.Linux.Processes;

internal enum ProcessPresence { Gone, Alive, Unknown }
internal sealed record ProcessFact(int Id, string Name, long? StartedAt, ProcessPresence Presence);

// Layer 1: OS observations only. Unknown is never silently converted to Gone.
internal static class ProcessFacts
{
    internal static IEnumerable<ProcessFact> Snapshot()
    {
        foreach (var process in Process.GetProcesses())
        {
            ProcessFact fact;
            using (process) fact = Read(process);
            yield return fact;
        }
    }
    internal static ProcessFact Read(int id)
    {
        try { using var process = Process.GetProcessById(id); return Read(process); }
        catch (ArgumentException) { return new(id, "", null, ProcessPresence.Gone); }
    }
    private static ProcessFact Read(Process process)
    {
        var name = ""; long? started = null;
        try
        {
            if (process.HasExited) return new(process.Id, name, started, ProcessPresence.Gone);
            name = process.ProcessName;
            started = process.StartTime.ToUniversalTime().Ticks;
            var presence = OperatingSystem.IsLinux() ? ParseStat(File.ReadAllText($"/proc/{process.Id}/stat")) : ProcessPresence.Alive;
            return new(process.Id, name, started, presence);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or DirectoryNotFoundException)
        { return new(process.Id, name, started, ProcessPresence.Gone); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { return new(process.Id, name, started, ProcessPresence.Unknown); }
    }
    internal static ProcessPresence ParseStat(string stat)
    {
        var end = stat.LastIndexOf(')');
        if (end < 0 || end + 2 >= stat.Length) return ProcessPresence.Unknown;
        return stat[end + 2] is 'Z' or 'X' or 'x' ? ProcessPresence.Gone : ProcessPresence.Alive;
    }
    internal static ProcessPresence Identify(int id, long startedAt)
    {
        if (id <= 0 || startedAt <= 0) return ProcessPresence.Unknown;
        var fact = Read(id);
        return fact.Presence == ProcessPresence.Gone || fact.StartedAt is long actual && actual != startedAt
            ? ProcessPresence.Gone : fact.Presence;
    }
    internal static string? EnvironmentOf(ProcessFact fact)
    {
        if (fact.Presence == ProcessPresence.Gone) return null;
        try
        {
            var environment = File.ReadAllText($"/proc/{fact.Id}/environ");
            // Avoid attaching another process's environment after PID reuse.
            return fact.StartedAt is long started && Identify(fact.Id, started) == ProcessPresence.Alive ? environment : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}
