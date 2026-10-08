using System.Diagnostics;

namespace XIVLauncher.Common.Unix;

public sealed record CompatibilitySettings
{
    public string Executable { get; init; } = string.Empty;
    // Shared Proton compatdata directory (contains pfx).
    public string DataDirectory { get; init; } = string.Empty;
    public string SteamRoot { get; init; } = string.Empty;
    public string? RuntimeEntryPoint { get; init; }
}

public sealed class CompatibilityRunner(CompatibilitySettings settings)
{
    public CompatibilitySettings Settings => settings;

    public void Validate()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("此入口目前仅支持 Linux x64。");
        if (!Path.IsPathRooted(settings.Executable) || !File.Exists(settings.Executable))
            throw new FileNotFoundException("请选择有效的 Proton 脚本。", settings.Executable);
        if (!Path.IsPathRooted(settings.DataDirectory)) throw new ArgumentException("兼容数据目录必须是绝对路径。");
        {
            if (!Directory.Exists(settings.SteamRoot)) throw new DirectoryNotFoundException("请选择 Steam 安装目录。");
            if (Path.GetFileName(settings.DataDirectory.TrimEnd('/')) == "pfx" || Directory.Exists(Path.Combine(settings.DataDirectory, "drive_c")))
                throw new ArgumentException("Proton 数据目录应是 pfx 的父目录，不是 Wine prefix 本身。请使用独立的新目录。");
            var proton = ProtonDiscovery.FromScript(settings.Executable, settings.SteamRoot);
            if (proton.RequiredRuntimeAppId != null && settings.RuntimeEntryPoint == null)
                throw new InvalidOperationException($"此 Proton 需要 Steam Linux Runtime {proton.RequiredRuntimeAppId}。请在 Steam 中安装后刷新列表。");
        }
        if (settings.RuntimeEntryPoint != null && !File.Exists(settings.RuntimeEntryPoint))
            throw new FileNotFoundException("Steam Linux Runtime 入口不存在。", settings.RuntimeEntryPoint);
    }

    public ProcessStartInfo BuildStartInfo(IEnumerable<string> arguments, string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null, bool mainSession = false, string protonVerb = "run")
    {
        var psi = new ProcessStartInfo
        {
            FileName = settings.RuntimeEntryPoint ?? settings.Executable,
            WorkingDirectory = workingDirectory ?? settings.DataDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (settings.RuntimeEntryPoint != null)
        {
            psi.ArgumentList.Add(mainSession ? "--verb=waitforexitandrun" : "--verb=run");
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(settings.Executable);
        }
        {
            // Do not use waitforexitandrun here: path conversion and injector share this prefix.
            psi.ArgumentList.Add(protonVerb);
            psi.Environment["STEAM_COMPAT_DATA_PATH"] = settings.DataDirectory;
            psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = settings.SteamRoot;
            psi.Environment["STEAM_COMPAT_INSTALL_PATH"] = workingDirectory ?? settings.DataDirectory;
            psi.Environment["STEAM_COMPAT_APP_ID"] = "0";
            psi.Environment["SteamAppId"] = "0";
            psi.Environment["SteamGameId"] = "0";
            psi.Environment["STEAM_COMPAT_TOOL_PATHS"] = string.Join(':',
                new[] { Path.GetDirectoryName(settings.Executable), Path.GetDirectoryName(settings.RuntimeEntryPoint) }.Where(x => !string.IsNullOrEmpty(x)));
            // SLR must be able to access external game/data libraries too.
            psi.Environment["STEAM_COMPAT_MOUNTS"] = string.Join(':',
                new[] { (psi.Environment.TryGetValue("STEAM_COMPAT_MOUNTS", out var mounts) ? mounts : null), settings.DataDirectory,
                    Path.GetDirectoryName(settings.DataDirectory), workingDirectory, Path.GetDirectoryName(settings.Executable), AppContext.BaseDirectory }
                    .Where(x => !string.IsNullOrEmpty(x)));
            psi.Environment.Remove("WINEPREFIX");
        }
        psi.Environment["WINEDEBUG"] = "-all";
        psi.Environment["LC_ALL"] = "C.UTF-8";
        psi.Environment["LANG"] = "C.UTF-8";
        if (environment != null)
            foreach (var (key, value) in environment)
                psi.Environment[key] = key == "STEAM_COMPAT_MOUNTS" ? (psi.Environment.TryGetValue(key, out var current) ? current : "") + ":" + value : value;
        if (environment != null && environment.TryGetValue("DOTNET_ROOT", out var windowsRuntime))
            psi.Environment["DOTNET_ROOT_X64"] = windowsRuntime;
        if (psi.Environment.TryGetValue("STEAM_COMPAT_MOUNTS", out var allMounts) && allMounts != null)
            psi.Environment["STEAM_COMPAT_MOUNTS"] = FilterMounts(allMounts);
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        return psi;
    }

    // pressure-vessel owns /usr and /etc; Proton tools there are exposed via TOOL_PATHS.
    public static string FilterMounts(string mounts) => string.Join(':', mounts.Split(':', StringSplitOptions.RemoveEmptyEntries)
        .Where(path => Path.IsPathRooted(path))
        .Select(Path.GetFullPath)
        .Where(path => !new[] { "/usr", "/etc", "/bin", "/sbin", "/lib", "/lib64" }
            .Any(reserved => path == reserved || path.StartsWith(reserved + "/", StringComparison.Ordinal)))
        .Distinct(StringComparer.Ordinal));

    public async Task<string> CaptureAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? environment = null, string protonVerb = "runinprefix")
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(settings.DataDirectory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var process = Process.Start(BuildStartInfo(arguments, environment: environment, protonVerb: protonVerb)) ?? throw new IOException("启动兼容工具失败。");
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0) throw new IOException($"Proton exited with code {process.ExitCode}: {error}");
            return output;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    // Steam must keep its entry process alive after the native window closes.
    // Proton's waitforexitandrun waits for this configured prefix, then executes a no-op.
    public async Task WaitForPrefixExitAsync()
    {
        using var process = Process.Start(BuildStartInfo(["cmd", "/c", "exit", "0"],
            protonVerb: "waitforexitandrun")) ?? throw new IOException("无法等待 Proton 会话结束。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        var diagnostic = await error;
        if (process.ExitCode != 0) throw new IOException("等待 Proton 会话退出失败：" + diagnostic);
    }

    public async Task<string> ToWindowsPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var output = await CaptureAsync(["winepath", "-w", Path.GetFullPath(path)], cancellationToken);
        var result = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        if (string.IsNullOrEmpty(result) || !(result.Length > 2 && result[1] == ':' || result.StartsWith(@"\\")))
            throw new IOException("winepath 未返回有效 Windows 路径。请先运行环境检测。");
        return result;
    }

    public async Task<string> DiagnoseAsync(CancellationToken cancellationToken = default)
    {
        Validate();
        // run initializes Proton's prefix files/DXVK and version marker; runinprefix skips it.
        // Do this before getting short-lived login tickets. Its stdout is not a handshake.
        await CaptureAsync(["cmd", "/c", "exit", "0"], cancellationToken,
                new Dictionary<string, string> { ["PROTON_LOG"] = "0" }, protonVerb: "run");
        var path = await ToWindowsPathAsync(settings.DataDirectory, cancellationToken);
        var echo = await CaptureAsync(["cmd", "/c", "echo", "SOIL_COMPAT_OK"], cancellationToken);
        if (!echo.Contains("SOIL_COMPAT_OK")) throw new IOException("兼容环境未能执行 Windows 命令。");
        return $"Windows command execution succeeded.\nData directory: {settings.DataDirectory}\nWine path: {path}";
    }
}
