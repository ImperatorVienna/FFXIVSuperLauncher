using XIVLauncher.Linux.Frontend;
using XIVLauncher.Linux.Processes;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class CleanupOwnershipTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"cleanup-owner-"+Guid.NewGuid());
    [Fact]
    public async Task HostStartupCleansAbandonedTemporarySessionWithoutUiRequest()
    {
        var directory=Path.Combine(root,"ffxiv_cn","game-sessions");Directory.CreateDirectory(directory);
        var old=Path.Combine(directory,"old.json.tmp");var recent=Path.Combine(directory,"recent.json.tmp");
        File.WriteAllText(old,"partial");File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddMinutes(-10));File.WriteAllText(recent,"partial");
        await FrontendHost.RunAsync(root,new StringReader(""),new StringWriter());
        Assert.False(File.Exists(old));Assert.True(File.Exists(recent));
    }
    [Fact]
    public void DeadOwnerMarkerIsRemovedButLiveAndUnknownOwnersAreProtected()
    {
        Directory.CreateDirectory(root);var marker=Path.Combine(root,"package.in-use");
        ProcessFileOwner.Publish(marker,Environment.ProcessId);Assert.True(ProcessFileOwner.IsProtected(marker));
        File.WriteAllText(marker,$"{Environment.ProcessId} 0");Assert.False(ProcessFileOwner.IsProtected(marker));Assert.False(File.Exists(marker));
        File.WriteAllText(marker,"legacy");Assert.True(ProcessFileOwner.IsProtected(marker));
    }
    [Fact]
    public void HelperOwnedLogIsSkippedUntilOwnerIsGone()
    {
        var directory=Path.Combine(root,"logs");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"launcher-20000101-000000-2147483647.log");File.WriteAllText(path,"data");
        ProcessFileOwner.Publish(path+".in-use",Environment.ProcessId);
        Assert.Equal(1,FrontendCleanup.Clean(root,"logs","",default).Skipped);Assert.True(File.Exists(path));
        File.WriteAllText(path+".in-use",$"{Environment.ProcessId} 0");
        Assert.Equal(1,FrontendCleanup.Clean(root,"logs","",default).Deleted);Assert.False(File.Exists(path));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
