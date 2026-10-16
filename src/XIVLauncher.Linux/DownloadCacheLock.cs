namespace XIVLauncher.Linux;

// All launcher download/install entry points and manual cache cleanup share this lease.
// Keep the lock file: deleting it would let two processes lock different inodes.
internal static class DownloadCacheLock
{
    internal static FileStream Acquire(string root)
    {
        Directory.CreateDirectory(root);
        try
        {
            return Platforms.PlatformServices.Storage.OpenPrivateFile(
                Path.Combine(root, ".download-cache.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }
        catch (IOException)
        {
            throw new CredentialValidationException("Download cache is busy.", "其他下载、安装或缓存清理正在进行，请稍后重试。");
        }
    }
}
