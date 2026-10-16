using System.Diagnostics;
using System.Reflection;
using XIVLauncher.Linux.Platforms.Windows;

// The outer process remains Steam's lifetime anchor after Chromium exits.
// Program files are shipped in a portable folder; updates wait for all owners to exit.
internal static class Program
{
    private static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new IOException("Windows runtime payload is missing.");
    private static string Script(string name) { using var stream = Resource(name); using var reader = new StreamReader(stream); return reader.ReadToEnd(); }
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 2;
        var root = Path.GetDirectoryName(Environment.ProcessPath ?? throw new IOException("Launcher executable path is unavailable."))!;
        if (args.Length == 2 && args[0] == "--install-update")
            return await WindowsUpdateJob.RunAsync(args[1], Script("Portable.Launch"));
        if (args.Length == 1 && args[0] == "--launcher-version")
        { Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)); return 0; }
        if (args.Length != 0) return 2;
        Directory.CreateDirectory(root);
        try
        {
            var executable = Environment.ProcessPath ?? throw new IOException("Launcher executable path is unavailable.");
            // Shortcut failure cannot make an otherwise usable launcher unstartable.
            try
            {
                WindowsPortableEntry.Write(executable, Script("Portable.Launch"));
                using var registration = Process.Start(WindowsPortableEntry.DesktopRegistration(Path.GetDirectoryName(executable)!, Script("Portable.Desktop")));
                if (registration != null)
                {
                    try { await registration.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
                    catch (TimeoutException) { if (!registration.HasExited) registration.Kill(); throw; }
                }
            }
            catch (Exception ex) { await File.AppendAllTextAsync(Path.Combine(root,"portable.log"), "Desktop entry: " + ex.Message + Environment.NewLine); }
            CleanupUpdateHelpers(root);
            var runtime = Path.Combine(root, "runtime");
            foreach (var required in new[] { "electron.exe", "xivlauncher-backend.exe", "frontend/main.cjs", "resources/app/main.cjs" })
                if (!File.Exists(Path.Combine(runtime, required))) throw new IOException("Incomplete Windows package: " + required);
            var session = Path.Combine(root,"sessions",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(session);
            try
            {
                var start = new ProcessStartInfo(Path.Combine(runtime,"electron.exe")) { UseShellExecute=false, WorkingDirectory=runtime };
                start.ArgumentList.Add(Path.Combine(runtime,"resources","app")); start.ArgumentList.Add("--super-launcher");
                start.Environment.Remove("ELECTRON_RUN_AS_NODE"); start.Environment.Remove("NODE_OPTIONS");
                start.Environment["FFXIV_PORTABLE_EXECUTABLE"] = executable;
                start.Environment["FFXIV_SESSION_DIRECTORY"] = session;
                start.Environment["FFXIV_SESSION_SUPERVISOR"] = Environment.ProcessId.ToString();
                using var electron = Process.Start(start) ?? throw new IOException("Could not start the interface.");
                Process? backend = null;
                try
                {
                    // Acquire a native process handle before acknowledging handoff.
                    // Electron does not close its backend pipe until this succeeds.
                    while (!electron.HasExited)
                    {
                        var request = Path.Combine(session,"backend");
                        if (backend == null && File.Exists(request))
                        {
                            var handoff = WindowsSessionHandoff.Parse(await File.ReadAllTextAsync(request), Environment.ProcessId);
                            var pid = handoff.ProcessId;
                            backend = Process.GetProcessById(pid);
                            _ = backend.Handle;
                            handoff.Validate(backend.MainModule?.FileName, Path.Combine(runtime,"xivlauncher-backend.exe"), executable,
                                handoff.PrimarySession == null ? null : await File.ReadAllTextAsync(Path.Combine(root,"electron-session.json")));
                            await File.WriteAllTextAsync(Path.Combine(session,"accepted"),pid.ToString());
                        }
                        await Task.Delay(100);
                    }
                    if (backend != null) await backend.WaitForExitAsync();
                    return electron.ExitCode;
                }
                finally { backend?.Dispose(); }
            }
            finally { try { Directory.Delete(session,true); } catch (IOException) { } }
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(Path.Combine(root,"portable.log"), ex + Environment.NewLine);
            return 1;
        }
    }
    private static void CleanupUpdateHelpers(string root)
    {
        // The helper cannot delete its own executable on Windows. Reap only
        // completed, precisely named helper directories on a later launch.
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, ".launcher-update-*"))
            {
                try
                {
                    if (!Guid.TryParseExact(Path.GetFileName(directory)[17..], "N", out _) ||
                        (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                        !File.Exists(Path.Combine(directory, "job.json.finished"))) continue;
                    Directory.Delete(directory, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

}
