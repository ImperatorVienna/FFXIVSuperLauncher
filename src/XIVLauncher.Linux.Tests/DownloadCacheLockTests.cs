using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class DownloadCacheLockTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cache-lock-test-" + Guid.NewGuid());
    [Fact]
    public void DownloadLeaseBlocksCleanupButNotLogCleanup()
    {
        Directory.CreateDirectory(Path.Combine(root,"ffxiv_cn","patches"));
        var patch = Path.Combine(root,"ffxiv_cn","patches","patch"); File.WriteAllText(patch,"keep");
        using (DownloadCacheLock.Acquire(root))
        {
            Assert.Throws<CredentialValidationException>(() => FrontendCleanup.Clean(root,"downloads","",default));
            FrontendCleanup.Clean(root,"logs","",default);
            Assert.True(File.Exists(patch));
        }
        FrontendCleanup.Clean(root,"downloads","",default);
        Assert.False(File.Exists(patch));
    }
    [Fact]
    public void CurrentLogCanBeClearedAndWrittenAgain()
    {
        var log = new SessionDiagnostics(root);log.Write("before","shared","INFO");
        FrontendCleanup.Clean(root,"logs","",default);
        Assert.Equal("",File.ReadAllText(log.FilePath));
        log.Write("after","shared","INFO");
        Assert.Contains("after",File.ReadAllText(log.FilePath));
        Assert.DoesNotContain("before",File.ReadAllText(log.FilePath));
    }
    [Fact]
    public void LeaseIsVisibleToAnotherProcess()
    {
        using (DownloadCacheLock.Acquire(root)) Assert.Equal(1, TryExternalLock());
        Assert.Equal(0, TryExternalLock());
        int TryExternalLock()
        {
            var start = new System.Diagnostics.ProcessStartInfo("flock") { UseShellExecute = false };
            foreach(var arg in new[]{"-n",Path.Combine(root,".download-cache.lock"),"true"}) start.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(start)!;
            Assert.True(process.WaitForExit(5000)); return process.ExitCode;
        }
    }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
