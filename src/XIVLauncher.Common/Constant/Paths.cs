namespace XIVLauncher.Common.Constant;

public static class Paths
{
    public static string RoamingPath { get; private set; } =
        Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"), "xivlauncher-super");

    public static void OverrideRoamingPath(string path) =>
        RoamingPath = Environment.ExpandEnvironmentVariables(path);
}
