using System.Diagnostics;
namespace XIVLauncher.Linux.Updates;

// Download/signature/hash verification is shared. Replacement and restart are
// platform operations; an unavailable installer must never fall back to Linux.
internal interface IUpdateInstaller
{
    bool CanInstall(string dataRoot);
    string LauncherPath { get; }
    Process Start(string stage, string sha256, string log, string version);
}
