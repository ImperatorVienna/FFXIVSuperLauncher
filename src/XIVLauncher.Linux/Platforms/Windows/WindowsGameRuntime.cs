using System.Diagnostics;
using System.Text;
using System.Text.Json;
using XIVLauncher.Dalamud;

namespace XIVLauncher.Linux.Platforms.Windows;

// A Windows PID is held as a Process handle. Never rediscover a game by name or
// attach to a reused PID after the originally launched process has exited.
internal sealed class WindowsGameRuntime : IGameRuntime
{
    private Process? game;
    public void Validate() { }
    public Task DiagnoseAsync(Action<string> status, CancellationToken token)
    { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public RegionDalamudFiles? WithoutDalamud(string region) => region == "ffxiv_cn"
        ? RegionLaunchPreparation.FixedChinaHelper(AppContext.BaseDirectory) : null;

    public Task<ProcessStartInfo> PrepareAsync(LinuxSettings settings, RegionDalamudFiles? files,
        FileInfo executable, DalamudStartInfo? info, bool noPlugins, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = BuildStartInfo(settings.SelectedRegion, executable.FullName, files, info, settings.EnableDalamud, noPlugins);
        if (settings.SelectedRegion == "ffxiv" && settings.Current.SteamAccount)
        {
            start.Environment["IS_FFXIV_LAUNCH_FROM_STEAM"] = "1";
            start.Environment["SteamAppId"] = settings.Current.FreeTrial ? "312060" : "39210";
            start.Environment["SteamGameId"] = start.Environment["SteamAppId"];
        }
        return Task.FromResult(start);
    }

    internal static ProcessStartInfo BuildStartInfo(string region, string executable, RegionDalamudFiles? files,
        DalamudStartInfo? info, bool enabled, bool noPlugins)
    {
        if (region is not ("ffxiv" or "ffxiv_cn" or "ffxiv_tc")) throw new ArgumentException("Unknown game version.");
        var start = new ProcessStartInfo(files?.Injector.FullName ?? executable)
        {
            UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)!,
            RedirectStandardOutput = files != null, RedirectStandardError = files != null, CreateNoWindow = files != null
        };
        if (files == null) return start;
        ArgumentNullException.ThrowIfNull(info);
        if (!files.Injector.Exists) throw new FileNotFoundException("Dalamud injector is missing.", files.Injector.FullName);
        foreach (var value in new[] {
            "launch", "--mode=entrypoint", "--dalamud-client-language=" + files.Language,
            "--game=" + executable, "--dalamud-working-directory=" + files.Injector.DirectoryName,
            "--dalamud-configuration-path=" + info.ConfigurationPath, "--logpath=" + info.LoggingPath,
            "--dalamud-plugin-directory=" + info.PluginDirectory, "--dalamud-asset-directory=" + info.AssetDirectory,
            "--dalamud-delay-initialize=" + info.DelayInitializeMs,
            "--dalamud-tspack-b64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(info.TroubleshootingPackData))
        }) start.ArgumentList.Add(value);
        if (region != "ffxiv") start.ArgumentList.Add("--launcher-directory=" + info.LauncherDirectory);
        if (files.Soil) start.ArgumentList.Add("--no-sandbox");
        if (!enabled) start.ArgumentList.Add(DalamudInjectorArgs.WITHOUT_DALAMUD);
        if (noPlugins) start.ArgumentList.Add(DalamudInjectorArgs.NO_PLUGIN);
        start.ArgumentList.Add("--");
        start.Environment["DALAMUD_RUNTIME"] = files.Runtime.FullName;
        start.Environment["DOTNET_ROOT"] = files.Runtime.FullName;
        start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        return start;
    }

    public async Task LaunchAsync(LinuxSettings settings, ProcessStartInfo start, string arguments, bool injected,
        Action<string> status, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // The authentication result is already a Windows command line; preserve it.
        start.Arguments = string.Join(' ', start.ArgumentList.Select(WindowsCommandLine.Quote)) + " " + arguments;
        start.ArgumentList.Clear();
        if (!injected)
        {
            game = Process.Start(start) ?? throw new IOException("Could not start the game.");
        }
        else
        {
            using var injector = new Process { StartInfo = start };
            var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stdout = new InjectorResponse(); var stderr = new InjectorResponse();
            injector.OutputDataReceived += (_, e) => { if (e.Data != null && stdout.Read(e.Data, out var pid)) ready.TrySetResult(pid); };
            injector.ErrorDataReceived += (_, e) => { if (e.Data != null && stderr.Read(e.Data, out var pid)) ready.TrySetResult(pid); };
            if (!injector.Start()) throw new IOException("Could not start the injector.");
            injector.BeginOutputReadLine(); injector.BeginErrorReadLine();
            var exited = injector.WaitForExitAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMinutes(4));
            try
            {
                if (await Task.WhenAny(ready.Task, exited).WaitAsync(timeout.Token) == exited)
                {
                    injector.WaitForExit(); // Drain asynchronous PID output before declaring failure.
                    if (!ready.Task.IsCompletedSuccessfully) throw new IOException($"Injector exited ({injector.ExitCode}) without confirming the game process.");
                }
                var pid = await ready.Task.WaitAsync(timeout.Token);
                game = Process.GetProcessById(pid);
                if (!Processes.ProcessIdentity.IsGameProcessName(game.ProcessName))
                { game.Dispose(); game = null; throw new IOException("Injector returned an unexpected process."); }
                _ = game.Handle; // Pin identity before PID reuse is possible.
            }
            catch
            {
                // Stop only our injector. A started game must never be killed as
                // collateral damage of a late cancellation or handshake failure.
                try { if (!injector.HasExited) { injector.Kill(); await exited; } } catch (InvalidOperationException) { }
                throw;
            }
        }
        if (game.HasExited) throw new IOException("The game exited immediately after launch.");
        status("Game started. Waiting for the game process to exit.");
    }
    public bool IsGameRunning => game != null && !game.HasExited;
    public bool ObserveGame() => IsGameRunning;
    public async Task WaitForExitAsync()
    {
        if (game == null) return;
        try { await game.WaitForExitAsync(); }
        finally { game.Dispose(); game = null; }
    }

    private sealed class InjectorResponse
    {
        private readonly StringBuilder pending = new();
        public bool Read(string line, out int pid)
        {
            pid = 0; var text = line.Trim().TrimStart('\uFEFF');
            if (text.StartsWith('{')) pending.Clear();
            if (pending.Length == 0 && !text.StartsWith('{')) return false;
            pending.AppendLine(text);
            if (pending.Length > 8192) { pending.Clear(); return false; }
            try
            {
                using var doc = JsonDocument.Parse(pending.ToString());
                return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("pid", out var value)
                    && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out pid) && pid > 0;
            }
            catch (JsonException) { return false; }
        }
    }
}
