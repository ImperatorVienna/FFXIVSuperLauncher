using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;
[CollectionDefinition("Session release process tests", DisableParallelization = true)]
public sealed class SessionReleaseProcessCollection { }

[Collection("Session release process tests")]
public sealed class FrontendSessionReleaseTests
{
    [Theory]
    [InlineData("failed",true)]
    [InlineData("cancelled",false)]
    [InlineData("completed",false)]
    [InlineData("completed",true)]
    public void NoSessionNeverReleases(string state,bool launched) =>
        Assert.False(FrontendSessionRelease.CanRelease(new("id","ffxiv",state,null,"",null,launched),null));
    [Fact] public void MatchRequiresExactPrefixNotSubstringOrOtherVariable()
    {
        Assert.True(FrontendSessionRelease.MatchesPrefix("OTHER=x\0WINEPREFIX=/data/pfx/\0", "/data/pfx"));
        Assert.False(FrontendSessionRelease.MatchesPrefix("WINEPREFIX=/data/pfx-other\0", "/data/pfx"));
        Assert.False(FrontendSessionRelease.MatchesPrefix("OTHER_WINEPREFIX=/data/pfx\0", "/data/pfx"));
        Assert.False(FrontendSessionRelease.MatchesPrefix("WINEPREFIX=relative\0", "/data/pfx"));
    }

    [Fact] public void OnlyLiveGameInSelectedPrefixAllowsCompletedLaunchRelease()
    {
        var root=Path.Combine(Path.GetTempPath(),"session-release-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
        var executable=Path.Combine(root,"ffxiv_dx11.exe");File.Copy("/usr/bin/sleep",executable);
        File.SetUnixFileMode(executable,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var psi=new System.Diagnostics.ProcessStartInfo(executable,"10") {UseShellExecute=false};
        psi.Environment["WINEPREFIX"]=Path.Combine(root,"pfx");
        using var process=System.Diagnostics.Process.Start(psi)!;
        try
        {
            Assert.True(SpinWait.SpinUntil(()=>FrontendSessionRelease.HasGame(Path.Combine(root,"pfx")),TimeSpan.FromSeconds(2)));
            Assert.False(FrontendSessionRelease.HasGame(Path.Combine(root,"other")));
            var runner=new XIVLauncher.Common.Unix.CompatibilityRunner(new(){DataDirectory=root});
            Assert.True(FrontendSessionRelease.CanRelease(new("id","ffxiv","completed",null,"",null,true),runner));
            Assert.False(FrontendSessionRelease.CanRelease(new("id","ffxiv","running",null,"",null,true),runner));
            process.Kill();process.WaitForExit();
            Assert.False(FrontendSessionRelease.CanRelease(new("id","ffxiv","completed",null,"",null,true),runner));
        }
        finally {if(!process.HasExited){process.Kill();process.WaitForExit();}Directory.Delete(root,true);}
    }
}
