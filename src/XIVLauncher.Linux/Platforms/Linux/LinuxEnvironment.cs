using System.Runtime.InteropServices;
namespace XIVLauncher.Linux.Platforms.Linux;
internal sealed class LinuxEnvironment : IPlatformEnvironment
{
    [DllImport("libc", SetLastError = true)] private static extern int setenv(string name, string value, int overwrite);
    public string BrowserPath(string root) => Path.Combine(root, "Tools/webview/electron");
    public string PatchWorkerPath(string root) => Path.Combine(root, "Tools/xdelta3");
    public void SetSteamApplication(uint appId)
    {
        var value = appId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (setenv("SteamAppId", value, 1) != 0 || setenv("SteamGameId", value, 1) != 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
    }
    public Processes.ProcessPresence ProcessPresence(int pid) => Processes.ProcessFacts.ParseStat(File.ReadAllText($"/proc/{pid}/stat"));
    public string? ReadProcessEnvironment(int pid) => File.ReadAllText($"/proc/{pid}/environ");
}
