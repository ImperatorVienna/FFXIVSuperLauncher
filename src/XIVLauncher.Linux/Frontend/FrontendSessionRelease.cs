using XIVLauncher.Linux.Platforms;
namespace XIVLauncher.Linux.Frontend;

internal static class FrontendSessionRelease
{
    internal static bool CanRelease(OperationView operation, IGameRuntime? runner) =>
        operation.State == "completed" && operation.Launched && runner != null &&
        runner.IsGameRunning;

    internal static bool HasGame(string prefix) =>
        Processes.ProcessSessions.Find(prefix, false) == Processes.ProcessPresence.Alive;
    internal static bool MatchesPrefix(string environment, string prefix) =>
        Processes.ProcessSessions.Matches(environment, "WINEPREFIX", prefix);
}
