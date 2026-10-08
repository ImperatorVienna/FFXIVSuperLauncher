using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using XIVLauncher.Dalamud;

namespace XIVLauncher.Common.Unix;

/// <summary>Starts the Windows injector inside the selected compatibility environment.
/// The returned PID is a Wine PID, never a System.Diagnostics.Process ID.</summary>
public sealed class LaunchUnconfirmedException(string message) : IOException(message);

public sealed class UnixDalamudRunner(CompatibilityRunner compatibility)
{
    public async Task<ProcessStartInfo> PrepareAsync(FileInfo injector, FileInfo gameExe,
        DirectoryInfo runtime, DalamudStartInfo info, bool enableDalamud, bool noPlugins,
        CancellationToken cancellationToken = default, int clientLanguage = 4, bool soil = true, bool includeLauncherDirectory = true)
    {
        compatibility.Validate();
        if (!injector.Exists) throw new FileNotFoundException("Dalamud 注入器不存在。", injector.FullName);
        var arguments = new List<string>
        {
            injector.FullName, "launch", "--mode=entrypoint", "--no-fix-acl",
            "--dalamud-client-language=" + clientLanguage,
            "--game=" + await compatibility.ToWindowsPathAsync(gameExe.FullName, cancellationToken),
            "--dalamud-working-directory=" + await compatibility.ToWindowsPathAsync(info.WorkingDirectory, cancellationToken),
            "--dalamud-configuration-path=" + await compatibility.ToWindowsPathAsync(info.ConfigurationPath, cancellationToken),
            "--logpath=" + await compatibility.ToWindowsPathAsync(info.LoggingPath, cancellationToken),
            "--dalamud-plugin-directory=" + await compatibility.ToWindowsPathAsync(info.PluginDirectory, cancellationToken),
            "--dalamud-asset-directory=" + await compatibility.ToWindowsPathAsync(info.AssetDirectory, cancellationToken),
            "--dalamud-delay-initialize=" + info.DelayInitializeMs,
            "--dalamud-tspack-b64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(info.TroubleshootingPackData))
        };
        if (includeLauncherDirectory) arguments.Add("--launcher-directory=" + await compatibility.ToWindowsPathAsync(info.LauncherDirectory, cancellationToken));
        arguments.Add("--dalamud-platform=linux");
        if (soil) arguments.Add("--no-sandbox");
        if (!enableDalamud) arguments.Add(DalamudInjectorArgs.WITHOUT_DALAMUD);
        if (noPlugins) arguments.Add(DalamudInjectorArgs.NO_PLUGIN);
        arguments.Add("--");
        var runtimePath = await compatibility.ToWindowsPathAsync(runtime.FullName, cancellationToken);
        var psi = compatibility.BuildStartInfo(arguments, gameExe.DirectoryName, new Dictionary<string, string>
        {
            ["DALAMUD_RUNTIME"] = runtimePath,
            ["DOTNET_ROOT"] = runtimePath,
            ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
            ["PROTON_LOG"] = "0",
            ["STEAM_LINUX_RUNTIME_LOG"] = "0",
            ["STEAM_COMPAT_MOUNTS"] = string.Join(':', injector.DirectoryName, runtime.FullName, info.AssetDirectory,
                info.LoggingPath, info.PluginDirectory, Path.GetDirectoryName(info.ConfigurationPath), info.LauncherDirectory)
        }, mainSession: true, protonVerb: "runinprefix");
        // Proton run routes through steam.exe, which can detach and lose injector stdout.
        // DiagnoseAsync initializes/upgrades the prefix before this direct Wine invocation.
        return psi;
    }

    public async Task<int> LaunchAsync(ProcessStartInfo psi, string gameArguments, CancellationToken cancellationToken = default, TimeSpan? confirmationTimeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Prepare paths BEFORE obtaining a short-lived game ticket.
        // Preserve the game's Windows command line; never pass it through a shell.
        psi.Arguments = string.Join(' ', psi.ArgumentList.Select(QuoteArgument)) + " " + gameArguments;
        psi.ArgumentList.Clear();
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new ConcurrentQueue<string>();
        var stdoutResponse = new ResponseReader();
        var stderrResponse = new ResponseReader();
        var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (stdoutResponse.Read(e.Data, out var pid)) ready.TrySetResult(pid);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null) return;
            if (stderrResponse.Read(e.Data, out var pid)) ready.TrySetResult(pid);
            errors.Enqueue(e.Data);
            while (errors.Count > 20) errors.TryDequeue(out _);
        };
        if (!process.Start()) { process.Dispose(); throw new IOException("无法启动注入器。"); }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _ = MonitorAsync();
        try { return await ready.Task.WaitAsync(confirmationTimeout ?? TimeSpan.FromMinutes(2), cancellationToken); }
        catch (TimeoutException)
        {
            // Do not kill a possibly-running game after an injector handshake timeout.
            throw new LaunchUnconfirmedException("Injector confirmation timed out. Check the game window and compatibility tool output.\n" + string.Join('\n', errors));
        }

        async Task MonitorAsync()
        {
            try
            {
                await process.WaitForExitAsync();
                process.WaitForExit(); // Flush asynchronous stdout events before reporting failure.
                var message = $"Injector exited with code {process.ExitCode}; no game PID confirmation was received.\n" + string.Join('\n', errors);
                ready.TrySetException(process.ExitCode == 0 ? new LaunchUnconfirmedException(message) : new IOException(message));
            }
            catch (Exception ex) { ready.TrySetException(ex); }
            finally { process.Dispose(); }
        }
    }

    public static bool TryReadWinePid(string line, out int pid)
    {
        pid = 0;
        try
        {
            using var doc = JsonDocument.Parse(line.Trim().TrimStart('\uFEFF'));
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("pid", out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out pid) && pid > 0;
        }
        catch (JsonException) { return false; }
    }

    private sealed class ResponseReader
    {
        private readonly StringBuilder pending = new();
        public bool Read(string line, out int pid)
        {
            if (TryReadWinePid(line, out pid)) return true;
            var text = line.Trim().TrimStart('\uFEFF');
            if (text.StartsWith('{')) pending.Clear();
            if (pending.Length == 0 && !text.StartsWith('{')) return false;
            pending.AppendLine(text);
            if (pending.Length > 8192) { pending.Clear(); return false; }
            if (!TryReadWinePid(pending.ToString(), out pid)) return false;
            pending.Clear(); return true;
        }
    }

    // Microsoft C runtime quoting, also understood by ProcessStartInfo on Unix.
    public static string QuoteArgument(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            slashes = 0;
            result.Append(c);
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
