using XIVLauncher.Linux.Frontend;
using XIVLauncher.Linux.Processes;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void UnknownUnrelatedProcessesDoNotExtendKnownGameLifetime()
    {
        var tracker=new GameSessionTracker();
        var unknown=new ProcessFact(20,"ffxiv_dx11.exe",200,ProcessPresence.Unknown);
        Assert.False(tracker.Update(new([], [unknown]), (_,_)=>ProcessPresence.Unknown));
        var game=new ProcessFact(10,"ffxiv_dx11.exe",100,ProcessPresence.Alive);
        Assert.True(tracker.Update(new([game], []), (_,_)=>ProcessPresence.Alive));
        Assert.True(tracker.Update(new([], [unknown]), (_,_)=>ProcessPresence.Unknown));
        Assert.False(tracker.Update(new([], [unknown]), (_,_)=>ProcessPresence.Gone));
    }
    [Fact]
    public async Task RecoveryDiscoveryDoesNotPerformHousekeeping()
    {
        var root=Path.Combine(Path.GetTempPath(),"discovery-boundary-"+Guid.NewGuid());
        var directory=Path.Combine(root,"ffxiv_cn","game-sessions");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"invalid.json");File.WriteAllText(path,"{");File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddHours(-1));
        try
        {
            var service=new GameSessionDiscovery(root);
            Assert.Null(await service.FindAsync(default));Assert.True(File.Exists(path));
            await service.CleanupAsync(default);Assert.False(File.Exists(path));
        }
        finally{Directory.Delete(root,true);}
    }
}
