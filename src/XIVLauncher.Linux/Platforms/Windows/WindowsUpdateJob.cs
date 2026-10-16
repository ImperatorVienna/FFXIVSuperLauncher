using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace XIVLauncher.Linux.Platforms.Windows;

// Data-only handoff to a copy of the portable host. No command text or user paths
// are interpolated into a shell, and the game is never restarted by this helper.
internal sealed record WindowsUpdateOwner(int Id, long StartedAt, string Executable);
internal sealed record WindowsUpdateJob(string Original, string Stage, string Version,
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
        new WindowsZipPackage().ValidateTarget(original);
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") || !System.Version.TryParse(version, out _))
            throw new IOException("Invalid update version.");
        var root = Path.GetDirectoryName(original)!;
        var job = new WindowsUpdateJob(original, stage, version,
            Hash(original), expected, log, owners);
        job.Validate();
        return job;
    }
    internal void Validate()
    {
        new WindowsZipPackage().ValidateTarget(Original); RegularFile(Stage);
        var root = Path.GetDirectoryName(Path.GetFullPath(Original));
        if (!Path.IsPathFullyQualified(Log) ||
            !string.Equals(Path.GetDirectoryName(Path.GetFullPath(Stage)), root, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Stage, Original, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Update must be staged beside the launcher.");
        if (!string.Equals(Hash(Original), OriginalHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Hash(Stage), ExpectedHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Update files changed after verification.");
        var package = WindowsZipPackage.Inspect(Stage);
        if (package.Version != Version) throw new IOException("Windows package version mismatch.");
    }
    internal static void CheckParents(string root, string target)
    {
        for (var path = target; path != null; path = Path.GetDirectoryName(path))
        {
            if (Path.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update cannot follow filesystem links.");
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new IOException("Update target is outside the launcher directory.");
    }
    internal void Install(string launchScript, Action<string> start)
    {
        Validate();
        var root = Path.GetDirectoryName(Original)!;
        CheckParents(root, Path.Combine(root, WindowsZipPackage.Inventory));
        using var oldFile = File.OpenRead(Path.Combine(root, WindowsZipPackage.Inventory));
        var previous = WindowsZipPackage.ReadManifest(oldFile); oldFile.Dispose();
        var work = Path.Combine(root, "update-backups", Guid.NewGuid().ToString("N"));
        CheckParents(root, work);
        var incoming = Path.Combine(work, "incoming");
        var backup = Path.Combine(work, "previous");
        Directory.CreateDirectory(incoming);
        var next = WindowsZipPackage.Inspect(Stage, incoming);
        var names = previous.Files.Keys.Union(next.Files.Keys, StringComparer.OrdinalIgnoreCase).Append(WindowsZipPackage.Inventory).ToArray();
        string Target(string name) => name == "FFXIVSuperLauncher.exe" ? Original : Path.Combine(root, name);
        foreach (var name in names) CheckParents(root, Target(name));
        var changed = new List<(string Target, string Backup, bool Existed)>();
        try
        {
            foreach (var name in names)
            {
                var target = Target(name); var saved = Path.Combine(backup, name);
                var existed = File.Exists(target);
                if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(saved)!); File.Move(target, saved); }
                changed.Add((target, saved, existed));
                var replacement = Path.Combine(incoming, name);
                if (File.Exists(replacement))
                { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Move(replacement, target); }
            }
            WindowsPortableEntry.Write(Original, launchScript);
            start(Original);
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var file in changed.AsEnumerable().Reverse())
            {
                try { File.Delete(file.Target); if (file.Existed) File.Move(file.Backup, file.Target); }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            try { WindowsPortableEntry.Write(Original, launchScript); }
            catch (Exception rollback) { errors.Add(rollback); }
            if (errors.Count > 1) throw new AggregateException("Windows update rollback requires recovery from " + backup, errors);
            throw;
        }
        // Keep the previous program files for recovery; user data was never in the plan.
        try { File.Delete(Stage); Directory.Delete(incoming, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
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
