using System.Diagnostics;
using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux.Frontend;

internal static class FrontendSessionRelease
{
    internal static bool CanRelease(OperationView operation, CompatibilityRunner? runner) =>
        operation.State == "completed" && operation.Launched && runner != null &&
        HasGame(Path.Combine(runner.Settings.DataDirectory, "pfx"));

    internal static bool HasGame(string prefix)
    {
        foreach (var process in Process.GetProcesses())
        using (process)
        {
            try
            {
                if (!GameProcessGuard.IsGameProcessName(process.ProcessName) || process.HasExited) continue;
                if (!GameProcessGuard.IsLiveLinuxStat(File.ReadAllText($"/proc/{process.Id}/stat"))) continue;
                if (MatchesPrefix(File.ReadAllText($"/proc/{process.Id}/environ"),prefix))return true;
            }
            catch (Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException) { }
        }
        return false;
    }
    internal static bool MatchesPrefix(string environment,string prefix) => environment.Split('\0').Any(value=>
        value.StartsWith("WINEPREFIX=",StringComparison.Ordinal) && Path.IsPathFullyQualified(value[11..]) &&
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(value[11..]))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(prefix)));
}
