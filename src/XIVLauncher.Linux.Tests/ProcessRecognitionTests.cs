using XIVLauncher.Linux.Processes;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class ProcessRecognitionTests
{
    [Theory]
    [InlineData("12 (name with ) bracket) S rest", 0)]
    [InlineData("12 (game) Z rest", 1)]
    [InlineData("invalid", 2)]
    public void StatDistinguishesUnknownFromExited(string stat, int expected) => Assert.Equal(new[]{ProcessPresence.Alive,ProcessPresence.Gone,ProcessPresence.Unknown}[expected],ProcessFacts.ParseStat(stat));
    [Fact]
    public void OwnerIdentityRejectsReusedPidAndKeepsUnknownIdentity()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        Assert.Equal(ProcessPresence.Alive, ProcessFacts.Identify(current.Id,current.StartTime.ToUniversalTime().Ticks));
        Assert.Equal(ProcessPresence.Gone, ProcessFacts.Identify(current.Id,1));
        Assert.Equal(ProcessPresence.Unknown, ProcessFacts.Identify(0,0));
    }
    [Fact]
    public void PrefixMatchingResolvesAliasesAndRejectsRelativeOrOtherVariables()
    {
        var root=Path.Combine(Path.GetTempPath(),"recognition-"+Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root,"real"));
        Directory.CreateSymbolicLink(Path.Combine(root,"alias"),Path.Combine(root,"real"));
        try
        {
            Assert.True(ProcessSessions.Matches("WINEPREFIX="+Path.Combine(root,"alias"),"WINEPREFIX",Path.Combine(root,"real")));
            Assert.False(ProcessSessions.Matches("WINEPREFIX=relative","WINEPREFIX",Path.Combine(root,"real")));
            Assert.False(ProcessSessions.Matches("OTHER_WINEPREFIX="+Path.Combine(root,"real"),"WINEPREFIX",Path.Combine(root,"real")));
        }
        finally { Directory.Delete(root,true); }
    }
}
