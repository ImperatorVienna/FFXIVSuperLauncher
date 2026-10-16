using System.Diagnostics;
using System.Text.Json;
using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms.Windows;

internal sealed class WindowsUpdateInstaller : IUpdateInstaller
{
    public string LauncherPath => Environment.GetEnvironmentVariable("FFXIV_PORTABLE_EXECUTABLE")
        ?? throw new IOException("No portable Windows launcher is supervising this session.");
    public bool CanInstall(string root) => OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable("FFXIV_PORTABLE_EXECUTABLE") is { Length: > 0 } file && File.Exists(file) &&
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(file)), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(Environment.GetEnvironmentVariable("FFXIV_SESSION_SUPERVISOR"), out var id) && id > 0;
    public Process Start(string stage, string sha256, string log, string version)
    {
        if (!CanInstall(Path.GetDirectoryName(LauncherPath)!)) throw new IOException("No Windows update supervisor.");
        using var backend = Process.GetCurrentProcess();
        using var supervisor = Process.GetProcessById(int.Parse(Environment.GetEnvironmentVariable("FFXIV_SESSION_SUPERVISOR")!));
        var host = WindowsUpdateJob.Owner(supervisor);
        if (!string.Equals(host.Executable, LauncherPath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected update supervisor.");
        var job = WindowsUpdateJob.Create(LauncherPath, stage, sha256, log, version, [WindowsUpdateJob.Owner(backend), host]);
        var directory = Path.Combine(Path.GetDirectoryName(LauncherPath)!, ".launcher-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Process? helper = null;
        try
        {
            // The helper is a self-contained folder application, not a copied single EXE.
            var root = Path.GetDirectoryName(LauncherPath)!;
            using var inventory = File.OpenRead(Path.Combine(root, WindowsZipPackage.Inventory));
            var manifest = WindowsZipPackage.ReadManifest(inventory);
            foreach (var name in manifest.Files.Keys.Where(name => !name.Contains('/')))
            {
                var source = name == "FFXIVSuperLauncher.exe" ? LauncherPath : Path.Combine(root, name);
                WindowsUpdateJob.CheckParents(root, source);
                File.Copy(source, Path.Combine(directory, name));
            }
            var executable = Path.Combine(directory, "FFXIVSuperLauncher.exe");
            var request = Path.Combine(directory, "job.json");
            File.WriteAllText(request, JsonSerializer.Serialize(job, WindowsUpdateJsonContext.Default.WindowsUpdateJob));
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
            start.ArgumentList.Add("--install-update"); start.ArgumentList.Add(request);
            helper = Process.Start(start) ?? throw new IOException("Could not start Windows update helper.");
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (helper.HasExited) throw new IOException("Windows update helper failed before shutdown.");
                if (File.Exists(request + ".ready")) return helper;
                Thread.Sleep(50);
            }
            throw new IOException("Windows update helper did not acknowledge readiness.");
        }
        catch
        {
            if (helper != null)
            {
                try { if (!helper.HasExited) { helper.Kill(); helper.WaitForExit(); } } finally { helper.Dispose(); }
            }
            try { Directory.Delete(directory, true); } catch (IOException) { }
            throw;
        }
    }
}
