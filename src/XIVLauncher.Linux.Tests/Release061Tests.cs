using Xunit;
namespace XIVLauncher.Linux.Tests;
public class Release061Tests
{
    [Fact] public void RoutinePluginMessagesAreSilent()
    {
        Assert.Equal("", UserLogMessage.Compact("已读取插件列表。"));
        Assert.Equal("", UserLogMessage.Compact("有未应用的修改。点击应用保存，或点击放弃修改。"));
        Assert.Equal("Failed: no changes.", UserLogMessage.Compact("Failed: no changes."));
    }
    [Fact] public void FailureSummaryDoesNotShowProcessOutputOrStack()
    {
        Assert.Equal("Failed: Injector exited.", UserLogMessage.Failure(new IOException("Injector exited.\nverbose process output\nmore output")));
        Assert.True(UserLogMessage.Compact(new string('x', 900)).Length <= 260);
        Assert.Equal("Failed: operation timed out.", UserLogMessage.Failure(new TimeoutException("verbose")));
        Assert.Equal("Operation cancelled.", UserLogMessage.Failure(new OperationCanceledException("verbose")));
        Assert.Equal("Failed: operation timed out.", UserLogMessage.Failure(new TaskCanceledException("verbose", new TimeoutException())));
    }
    [Fact] public void DiagnosticFileKeepsDetailsOutsideUiAndUsesPrivatePermissions()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var log = new SessionDiagnostics(root);
            log.Write("line one\nline two", "ffxiv_cn", "DETAIL");
            Assert.Contains("line two", File.ReadAllText(log.FilePath));
            Assert.Contains("[ffxiv_cn]", File.ReadAllText(log.FilePath));
            Assert.Null(log.WriteError);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(log.FilePath));
            var ui = new LauncherLog(); ui.Append("Result"); ui.Clear();
            Assert.Contains("line two", File.ReadAllText(log.FilePath));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void FileWriteFailureDoesNotBreakAnOperation()
    {
        var root = Path.GetTempFileName();
        try { var log = new SessionDiagnostics(root); log.Write("detail", "shared", "DETAIL"); Assert.NotNull(log.WriteError); }
        finally { File.Delete(root); }
    }
}
