using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms;

// Shared flows use these interfaces; this release supplies Linux adapters only.
internal static class PlatformServices
{
    static PlatformServices()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This release supports Linux only.");
    }
    internal static void ValidateSetup(LinuxSettings settings) => Linux.LinuxGameRuntime.ValidateSetup(settings);
    internal static void EnsureWebVerificationAvailable() => _ = WebviewRuntime.FindBrowser();
    internal static IPlatformStorage Storage { get; } = new Linux.LinuxStorage();
    internal static PlatformCapabilities Capabilities { get; } = new(true);
    internal static IGameRuntime CreateGameRuntime(LinuxSettings settings) => new Linux.LinuxGameRuntime(settings);
    internal static IUpdateInstaller? UpdateInstaller { get; } = new Linux.AppImageInstaller();
    internal static IUpdatePackage UpdatePackage { get; } = new Linux.AppImagePackage();
}
