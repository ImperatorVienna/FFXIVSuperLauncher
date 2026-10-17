namespace XIVLauncher.Linux.Platforms;
internal interface IPlatformEnvironment
{
    string BrowserPath(string root);
    string PatchWorkerPath(string root);
    void SetSteamApplication(uint appId);
    Processes.ProcessPresence ProcessPresence(int pid);
    string? ReadProcessEnvironment(int pid);
}
