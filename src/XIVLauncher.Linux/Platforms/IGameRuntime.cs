using System.Diagnostics;
using XIVLauncher.Dalamud;
namespace XIVLauncher.Linux.Platforms;

internal interface IGameRuntime
{
    void Validate();
    Task DiagnoseAsync(Action<string> status, CancellationToken token);
    RegionDalamudFiles? WithoutDalamud(string region);
    Task<ProcessStartInfo> PrepareAsync(LinuxSettings settings, RegionDalamudFiles? files, FileInfo game, DalamudStartInfo? info, bool noPlugins, CancellationToken token);
    Task LaunchAsync(LinuxSettings settings, ProcessStartInfo prepared, string arguments, bool injected, Action<string> status, CancellationToken token);
    bool IsGameRunning { get; }
    bool ObserveGame();
    Task WaitForExitAsync();
}
