using System.Diagnostics;
using System.Text;
namespace XIVLauncher.Linux.Platforms.Windows;

// Called by the outer portable executable, never by an extracted backend.
// Only files beside that EXE and the user's desktop shortcut are written.
internal static class WindowsPortableEntry
{
    internal static string EncodedCommand(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    internal static string Batch(string launchScript) => "@echo off\r\nsetlocal DisableDelayedExpansion\r\n" +
        "set \"FFXIV_LAUNCHER_ROOT=%~dp0\"\r\n" +
        "\"%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoLogo -NoProfile -NonInteractive -EncodedCommand " +
        EncodedCommand(launchScript) + "\r\nif errorlevel 1 (\r\n  pause\r\n  exit /b 1\r\n)\r\n";
    internal static void Write(string executable, string launchScript)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || new FileInfo(executable).LinkTarget != null)
            throw new IOException("Portable launcher path must identify a regular EXE.");
        var root = Path.GetDirectoryName(executable)!;
        AtomicWrite(Path.Combine(root, "launcher-executable.txt"), Path.GetFileName(executable));
        AtomicWrite(Path.Combine(root, "launch.bat"), Batch(launchScript));
    }
    private static void AtomicWrite(string path, string text)
    {
        var stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(stage, text, new UTF8Encoding(false)); File.Move(stage, path, true); }
        finally { if (File.Exists(stage)) File.Delete(stage); }
    }
    internal static ProcessStartInfo DesktopRegistration(string root, string script)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", EncodedCommand(script) }) start.ArgumentList.Add(arg);
        start.Environment["FFXIV_LAUNCHER_ROOT"] = root;
        return start;
    }
}
