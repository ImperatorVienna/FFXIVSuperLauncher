namespace XIVLauncher.Linux.Platforms.Windows;

internal sealed class WindowsStorage : IPlatformStorage
{
    public string DataRoot => PortablePaths.DataRoot;
    public string SettingsFileName => "windows-settings.json";
    // Inherit the portable directory ACL; Unix modes are not meaningful here.
    public DirectoryInfo CreatePrivateDirectory(string path) => Directory.CreateDirectory(path);
    public FileStream OpenPrivateFile(string path, FileMode mode, FileAccess access, FileShare share = FileShare.None) => new(path, mode, access, share);
    public void PreserveExecutable(string source, string target) { }
}
