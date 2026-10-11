using XIVLauncher.Linux.Updates;

namespace XIVLauncher.Linux.Frontend;

internal sealed record CleanupRequest(string Kind);

internal static class FrontendCleanup
{
    internal static object Start(string root, CleanupRequest request, FrontendOperations operations)
    {
        if (request.Kind is not ("downloads" or "logs")) throw new ArgumentException("Unknown cleanup kind.");
        return operations.Start(LinuxSettings.Load(root).SelectedRegion, operation => Task.Run(() =>
        {
            operation.SetResult(Clean(root, request.Kind, UpdateInstallation.LauncherPath, operation.Token));
        }, operation.Token));
    }

    internal static CleanupResult Clean(string root, string kind, string image, CancellationToken token) =>
        LauncherCleanupService.Clean(root, kind, image, token);
}
