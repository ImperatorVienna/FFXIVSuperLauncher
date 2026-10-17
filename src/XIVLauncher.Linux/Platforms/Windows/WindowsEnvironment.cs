namespace XIVLauncher.Linux.Platforms.Windows;
internal sealed class WindowsEnvironment : IPlatformEnvironment
{
    public string BrowserPath(string root) => Path.Combine(root, "electron.exe");
    public string PatchWorkerPath(string root) => Path.Combine(root, "Tools/xdelta3.exe");
    public void SetSteamApplication(uint appId)
    {
        var value = appId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("SteamAppId", value);
        Environment.SetEnvironmentVariable("SteamGameId", value);
    }
    // Called only after the shared Process handle check. No /proc emulation on Windows.
    public Processes.ProcessPresence ProcessPresence(int pid) => Processes.ProcessPresence.Alive;
    public string? ReadProcessEnvironment(int pid) => null;
}
