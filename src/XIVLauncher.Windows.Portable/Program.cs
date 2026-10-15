using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using XIVLauncher.Linux.Platforms.Windows;

// The outer process remains Steam's lifetime anchor after Chromium exits.
// Runtime payloads are versioned by content hash; never overwrite a running DLL.
internal static class Program
{
    private static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new IOException("Windows runtime payload is missing.");
    private static string Script(string name) { using var stream = Resource(name); using var reader = new StreamReader(stream); return reader.ReadToEnd(); }
    private static async Task<int> Main()
    {
        if (!OperatingSystem.IsWindows()) return 2;
        var root = Path.GetDirectoryName(Environment.ProcessPath ?? throw new IOException("Launcher executable path is unavailable."))!;
        Directory.CreateDirectory(root);
        try
        {
            var executable = Environment.ProcessPath ?? throw new IOException("Launcher executable path is unavailable.");
            // Shortcut failure cannot make an otherwise usable launcher unstartable.
            try
            {
                WindowsPortableEntry.Write(executable, Script("Portable.Launch"));
                using var registration = Process.Start(WindowsPortableEntry.DesktopRegistration(Path.GetDirectoryName(executable)!, Script("Portable.Desktop")));
                if (registration != null) await registration.WaitForExitAsync();
            }
            catch (Exception ex) { await File.AppendAllTextAsync(Path.Combine(root,"portable.log"), "Desktop entry: " + ex.Message + Environment.NewLine); }
            var runtime = Extract(root);
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
                            if (!int.TryParse(await File.ReadAllTextAsync(request),out var pid) || pid <= 0) throw new IOException("Invalid backend handoff.");
                            backend = Process.GetProcessById(pid);
                            _ = backend.Handle;
                            var image = backend.MainModule?.FileName;
                            if (!string.Equals(image,Path.Combine(runtime,"xivlauncher-backend.exe"),StringComparison.OrdinalIgnoreCase))
                                throw new IOException("Unexpected session process.");
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
    private static string Extract(string root)
    {
        using var payload = Resource("Portable.Payload");
        var digest = Convert.ToHexString(SHA256.HashData(payload)); payload.Position=0;
        var runtimes = Path.Combine(root,"runtime"); Directory.CreateDirectory(runtimes);
        var destination = Path.Combine(runtimes,digest);
        using var mutex = new Mutex(false,"Local\\FFXIVSuperLauncher.Extract."+digest);
        try { if (!mutex.WaitOne(TimeSpan.FromMinutes(2))) throw new IOException("Runtime extraction is busy."); }
        catch (AbandonedMutexException) { }
        try
        {
            if (File.Exists(Path.Combine(destination,".complete"))) return destination;
            var stage = destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(stage);
                ZipFile.ExtractToDirectory(payload,stage); // Framework rejects path traversal.
                foreach (var required in new[]{"electron.exe","xivlauncher-backend.exe","frontend/main.cjs","resources/app/main.cjs"})
                    if (!File.Exists(Path.Combine(stage,required))) throw new IOException("Incomplete Windows payload: "+required);
                File.WriteAllText(Path.Combine(stage,".complete"),digest);
                Directory.Move(stage,destination);
            }
            finally { if(Directory.Exists(stage))Directory.Delete(stage,true); }
            return destination;
        }
        finally { mutex.ReleaseMutex(); }
    }
}
