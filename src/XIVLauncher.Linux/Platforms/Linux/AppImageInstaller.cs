using System.Diagnostics;
using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms.Linux;

internal sealed class AppImageInstaller : IUpdateInstaller
{
    public bool CanInstall(string dataRoot) => Path.GetFullPath(dataRoot) == Path.GetFullPath(LinuxSettings.DataRoot)
        && Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 };
    public string LauncherPath => UpdateInstallation.LauncherPath;
    public Process Start(string stage, string sha256, string log, string version) =>
        UpdateInstallation.Start(LauncherPath, stage, sha256, log, version);
}
