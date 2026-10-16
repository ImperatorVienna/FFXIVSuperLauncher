namespace XIVLauncher.Linux.Platforms;

internal static class PortablePaths
{
    internal static string Resolve(string? executable, string fallback)
    {
        if (string.IsNullOrWhiteSpace(executable)) return Path.GetFullPath(fallback);
        if (!Path.IsPathFullyQualified(executable)) throw new IOException("Launcher executable path must be absolute.");
        return Path.GetDirectoryName(Path.GetFullPath(executable))!;
    }
    internal static string DataRoot => Resolve(Environment.GetEnvironmentVariable("APPIMAGE"), AppContext.BaseDirectory);
}
