namespace XIVLauncher.Linux.Platforms.Linux;
internal sealed class LinuxStorage : IPlatformStorage
{
    public string DataRoot => PortablePaths.DataRoot;
    public string SettingsFileName => "linux-settings.json";
    public DirectoryInfo CreatePrivateDirectory(string path) => Directory.CreateDirectory(path,
        UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    public FileStream OpenPrivateFile(string path,FileMode mode,FileAccess access,FileShare share=FileShare.None) => new(path,
        new FileStreamOptions{Mode=mode,Access=access,Share=share,UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite});
    public void PreserveExecutable(string source,string target)
    {
        if((File.GetUnixFileMode(source)&(UnixFileMode.UserExecute|UnixFileMode.GroupExecute|UnixFileMode.OtherExecute))!=0)
            File.SetUnixFileMode(target,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    }
}
