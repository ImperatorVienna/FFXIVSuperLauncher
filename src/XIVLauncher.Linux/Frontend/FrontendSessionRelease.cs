using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux.Frontend;

internal static class FrontendSessionRelease
{
    internal static bool CanRelease(OperationView operation, CompatibilityRunner? runner) =>
        operation.State == "completed" && operation.Launched && runner != null &&
        HasGame(Path.Combine(runner.Settings.DataDirectory, "pfx"));

    internal static bool HasGame(string prefix) =>
        Processes.ProcessSessions.Find(prefix, false) == Processes.ProcessPresence.Alive;
    internal static bool MatchesPrefix(string environment, string prefix) =>
        Processes.ProcessSessions.Matches(environment, "WINEPREFIX", prefix);
}
