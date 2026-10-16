namespace XIVLauncher.Linux.Platforms;
internal interface IPlatformStorage
{
    string DataRoot { get; }
    string SettingsFileName { get; }
    DirectoryInfo CreatePrivateDirectory(string path);
    FileStream OpenPrivateFile(string path, FileMode mode, FileAccess access, FileShare share = FileShare.None);
    void PreserveExecutable(string source, string target);
}
