using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms;

// The platform is selected once, outside shared business flows. Never silently
// use Linux behavior on an unsupported OS.
internal static class PlatformServices
{
    internal static IPlatformEnvironment Environment { get; } = OperatingSystem.IsLinux()
        ? new Linux.LinuxEnvironment() : OperatingSystem.IsWindows() ? new Windows.WindowsEnvironment() : throw new PlatformNotSupportedException();
    internal static FileSystemPathRules Paths { get; } = OperatingSystem.IsLinux()
        ? FileSystemPathRules.Linux : OperatingSystem.IsWindows() ? FileSystemPathRules.Windows : throw new PlatformNotSupportedException();
    internal static void ValidateSetup(LinuxSettings settings)
    {
        if (OperatingSystem.IsLinux()) Linux.LinuxGameRuntime.ValidateSetup(settings);
        else if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        // Native Windows setup does not require Steam or a compatibility runtime.
    }
    internal static void EnsureWebVerificationAvailable()
    {
        // Windows uses the launcher's existing Electron verification view.
        if (OperatingSystem.IsLinux()) _ = WebviewRuntime.FindBrowser();
    }
    internal static IPlatformStorage Storage { get; } = OperatingSystem.IsLinux()
        ? new Linux.LinuxStorage() : OperatingSystem.IsWindows() ? new Windows.WindowsStorage() : throw new PlatformNotSupportedException("No storage adapter is registered for this platform.");
    internal static PlatformCapabilities Capabilities { get; } = new(OperatingSystem.IsLinux());
    internal static IGameRuntime CreateGameRuntime(LinuxSettings settings) => OperatingSystem.IsLinux()
        ? new Linux.LinuxGameRuntime(settings) : OperatingSystem.IsWindows() ? new Windows.WindowsGameRuntime() : throw new PlatformNotSupportedException("No game runtime adapter is registered for this platform.");
    // Download/signature validation stays shared; installers own platform replacement and restart.
    internal static IUpdateInstaller? UpdateInstaller { get; } = OperatingSystem.IsLinux() ? new Linux.AppImageInstaller() : OperatingSystem.IsWindows() ? new Windows.WindowsUpdateInstaller() : null;
    internal static IUpdatePackage UpdatePackage { get; } = OperatingSystem.IsLinux()
        ? new Linux.AppImagePackage()
        : OperatingSystem.IsWindows() ? new Windows.WindowsZipPackage() : throw new PlatformNotSupportedException("No update package adapter is registered for this platform.");
}
