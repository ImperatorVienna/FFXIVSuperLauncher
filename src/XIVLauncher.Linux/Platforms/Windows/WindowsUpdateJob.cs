using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace XIVLauncher.Linux.Platforms.Windows;

// Data-only handoff to a copy of the portable host. No command text or user paths
// are interpolated into a shell, and the game is never restarted by this helper.
internal sealed record WindowsUpdateOwner(int Id, long StartedAt, string Executable);
internal sealed record WindowsUpdateJob(string Original, string Stage, string Destination,
    string OriginalHash, string ExpectedHash, string Log, WindowsUpdateOwner[] Owners)
{
    internal static string Hash(string file)
    { using var input = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(input)); }
    internal static void RegularFile(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Update requires a regular file at an absolute path.");
    }
    internal static WindowsUpdateOwner Owner(Process process)
    {
        _ = process.Handle;
        return new(process.Id, process.StartTime.ToUniversalTime().Ticks,
            process.MainModule?.FileName ?? throw new IOException("Cannot identify update owner."));
    }
    internal static WindowsUpdateJob Create(string original, string stage, string expected, string log,
        string version, WindowsUpdateOwner[] owners)
    {
        new WindowsExecutablePackage().ValidateTarget(original);
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") || !Version.TryParse(version, out _))
            throw new IOException("Invalid update version.");
        var root = Path.GetDirectoryName(original)!;
        var job = new WindowsUpdateJob(original, stage, Path.Combine(root, $"FFXIVSuperLauncher-{version}-win-x64.exe"),
            Hash(original), expected, log, owners);
        job.Validate();
        return job;
    }
    internal void Validate()
    {
        new WindowsExecutablePackage().ValidateTarget(Original); RegularFile(Stage);
        var root = Path.GetDirectoryName(Path.GetFullPath(Original));
        if (!Path.IsPathFullyQualified(Destination) || !Path.IsPathFullyQualified(Log) ||
            !string.Equals(Path.GetDirectoryName(Path.GetFullPath(Stage)), root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(Path.GetFullPath(Destination)), root, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Stage, Original, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Stage, Destination, StringComparison.OrdinalIgnoreCase) || Path.Exists(Destination))
            throw new IOException("Update files must be distinct and staged beside the portable launcher.");
        if (!string.Equals(Hash(Original), OriginalHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Hash(Stage), ExpectedHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Update files changed after verification.");
        new WindowsExecutablePackage().CompleteStage(Stage);
    }
    // The original stays available until the new launcher starts. On any failure
    // restore the entry and leave the old executable usable for retry/rollback.
    internal void Install(string launchScript, Action<string> start)
    {
        Validate();
        var backup = Original + ".previous";
        if (Path.Exists(backup) && (File.GetAttributes(backup) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Update backup cannot be a link.");
        File.Move(Stage, Destination);
        try
        {
            File.Copy(Original, backup, overwrite: true);
            WindowsPortableEntry.Write(Destination, launchScript);
            start(Destination);
        }
        catch
        {
            WindowsPortableEntry.Write(Original, launchScript);
            File.Move(Destination, Stage);
            throw;
        }
        // A virus scanner may briefly hold the old file; it is safe to retain it.
        try { File.Delete(Original); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    internal static async Task<int> RunAsync(string jobFile, string launchScript)
    {
        WindowsUpdateJob? job = null;
        var owners = new List<Process>();
        try
        {
            RegularFile(jobFile);
            job = JsonSerializer.Deserialize(await File.ReadAllTextAsync(jobFile), WindowsUpdateJsonContext.Default.WindowsUpdateJob)
                ?? throw new IOException("Missing update job.");
            job.Validate();
            if (job.Owners.Length != 2 || job.Owners.Any(o => o.Id <= 0 || o.StartedAt <= 0) || job.Owners[0].Id == job.Owners[1].Id)
                throw new IOException("Invalid update process owners.");
            foreach (var owner in job.Owners)
            {
                var process = Process.GetProcessById(owner.Id); owners.Add(process);
                var actual = Owner(process);
                if (actual.StartedAt != owner.StartedAt || !string.Equals(actual.Executable, owner.Executable, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Update owner identity changed.");
            }
            await File.WriteAllTextAsync(jobFile + ".ready", "ready");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await Task.WhenAll(owners.Select(p => p.WaitForExitAsync(timeout.Token)));
            job.Install(launchScript, executable =>
            {
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
                foreach (var key in new[] { "FFXIV_PORTABLE_EXECUTABLE", "FFXIV_SESSION_DIRECTORY", "FFXIV_SESSION_SUPERVISOR", "ELECTRON_RUN_AS_NODE", "NODE_OPTIONS" })
                    start.Environment.Remove(key);
                using var next = Process.Start(start) ?? throw new IOException("Could not restart updated launcher.");
            });
            return 0;
        }
        catch (Exception ex)
        {
            if (job != null)
                try { await File.AppendAllTextAsync(job.Log, "Windows update failed: " + ex + Environment.NewLine); } catch (IOException) { }
            return 1;
        }
        finally
        {
            foreach (var owner in owners) owner.Dispose();
            try { await File.WriteAllTextAsync(jobFile + ".finished", "finished"); } catch (IOException) { }
        }
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(WindowsUpdateJob))]
internal partial class WindowsUpdateJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
