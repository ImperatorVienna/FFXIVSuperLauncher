namespace XIVLauncher.Common.Constant;

public static class Paths
{
    private static string? roamingPath;
    public static string RoamingPath => roamingPath ?? throw new InvalidOperationException("The launcher must initialize its data directory first.");

    public static void OverrideRoamingPath(string path) =>
        roamingPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
}
