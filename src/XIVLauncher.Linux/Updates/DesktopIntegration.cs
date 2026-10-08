namespace XIVLauncher.Linux.Updates;
internal static class DesktopIntegration
{
    public static void PrepareAppImageStartup(string image, string dataRoot, string icon)
    {
        // Refresh creates default configuration first and never overwrites existing preferences.
        var entry = AppImageEntry.Refresh(image, Path.Combine(dataRoot, "xivlauncher-super"));
        Install(entry, dataRoot, icon);
    }

    public static string DataRoot => Path.GetDirectoryName(LinuxSettings.DataRoot)!;

    public static Task<string[]> RefreshCachesAsync() => Task.Run(async () =>
    {
        var errors = new List<string>();
        var applications = Path.Combine(DataRoot, "applications");
        if (Directory.Exists(applications))
            await RunAsync("update-desktop-database", [applications]);
        if ((Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").Split(':').Contains("KDE", StringComparer.OrdinalIgnoreCase))
        {
            var kde = Find("kbuildsycoca6") ?? Find("kbuildsycoca5");
            if (kde != null) await RunAsync(kde, []);
        }
        return errors.ToArray();

        async Task RunAsync(string command, string[] arguments)
        {
            var executable = Path.IsPathFullyQualified(command) ? command : Find(command);
            if (executable == null) return; // Optional desktop utilities vary by distribution.
            try
            {
                await RunCacheCommandAsync(executable, arguments, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex) { errors.Add($"Desktop cache refresh failed ({Path.GetFileName(command)}): {ex.Message}"); }
        }
    });

    internal static async Task RunCacheCommandAsync(string executable, string[] arguments, TimeSpan timeout)
    {
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start) ?? throw new IOException("Cannot start desktop cache refresh.");
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            var output = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, deadline.Token);
            var error = process.StandardError.BaseStream.CopyToAsync(Stream.Null, deadline.Token);
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), output, error).WaitAsync(deadline.Token);
            if (process.ExitCode != 0) throw new IOException($"Exit code {process.ExitCode}.");
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    private static string? Find(string name) => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
        .Where(Path.IsPathFullyQualified).Select(directory => Path.Combine(directory, name))
        .FirstOrDefault(path => File.Exists(path) && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0);

    public static void Install(string image, string dataRoot, string icon)
    {
        LauncherUpdates.ValidateTarget(image);
        if (image.Contains('\n') || image.Contains('\r')) throw new IOException("The AppImage path cannot contain a newline.");
        var applications = Path.Combine(dataRoot, "applications");
        var iconPath = Path.Combine(dataRoot, "icons/hicolor/500x500/apps/xivlauncher-super.png");
        Directory.CreateDirectory(applications); Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
        File.Copy(icon, iconPath, true);
        // Desktop Exec uses its own quoting rules, not shell parsing. % is a field-code escape.
        var escaped = image.Replace("\\", "\\\\\\\\").Replace("\"", "\\\\\"").Replace("`", "\\\\`").Replace("$", "\\\\$").Replace("%", "%%");
        using var template = typeof(DesktopIntegration).Assembly.GetManifestResourceStream("Desktop.Entry")
            ?? throw new IOException("Desktop entry template is missing.");
        using var reader = new StreamReader(template);
        // Use the persistent file directly: 500x500 is not a standard hicolor theme directory,
        // so a symbolic icon name cannot reliably be resolved by desktop menus or taskbars.
        var desktopIcon = iconPath.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        var content = reader.ReadToEnd().Replace("Exec=xivlauncher-super", "Exec=\"" + escaped + "\"")
            .Replace("Icon=xivlauncher-super", "Icon=" + desktopIcon);
        var file = Path.Combine(applications, "xivlauncher-super.desktop");
        File.WriteAllText(file + ".tmp", content); File.Move(file + ".tmp", file, true);
    }
}
