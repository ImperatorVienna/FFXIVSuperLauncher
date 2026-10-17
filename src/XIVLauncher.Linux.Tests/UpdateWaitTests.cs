using XIVLauncher.Linux.Frontend;
using XIVLauncher.Common.Runtime;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class UpdateWaitTests
{
    [Fact] public async Task SkipWaitsForUpdateCleanup()
    {
        var cleaned = false;
        var result = await UpdateWait.RunAsync<int>(async ct => { try { await Task.Delay(Timeout.Infinite, ct); return 1; } finally { await Task.Delay(20); cleaned = true; } },
            (_, _) => Task.FromResult("disable"), _ => {}, default, TimeSpan.FromMilliseconds(10));
        Assert.True(cleaned); Assert.Equal("disable", result.Choice);
    }
    [Fact] public async Task CompletionClosesPendingPrompt()
    {
        var work = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = false;
        var result = await UpdateWait.RunAsync(_ => work.Task, async (_, ct) => {
            work.SetResult(42);
            try { await Task.Delay(Timeout.Infinite, ct); return "disable"; }
            finally { closed = true; }
        }, _ => {}, default, TimeSpan.FromMilliseconds(10));
        Assert.Equal(42, result.Value); Assert.Null(result.Choice); Assert.True(closed);
    }
    [Fact] public async Task ContinueUsesSameWorker()
    {
        var calls = 0; var work = new TaskCompletionSource<int>();
        var result = await UpdateWait.RunAsync(_ => { calls++; return work.Task; }, (_, _) => { work.SetResult(7); return Task.FromResult("retry"); }, _ => {}, default, TimeSpan.FromMilliseconds(10));
        Assert.Equal(1, calls); Assert.Equal(7, result.Value);
    }
    [Fact] public async Task TimeoutRetryStartsAnotherAttempt()
    {
        var calls = 0;
        var result = await UpdateWait.RunAsync(_ => ++calls == 1 ? Task.FromException<int>(new TimeoutException()) : Task.FromResult(9),
            (timeout, _) => { Assert.True(timeout); return Task.FromResult("retry"); }, _ => {}, default);
        Assert.Equal(2, calls); Assert.Equal(9, result.Value);
    }
    [Fact] public async Task UserCancellationDrainsWritesWithoutFallback()
    {
        using var cancel = new CancellationTokenSource(); var cleaned = false;
        var task = UpdateWait.RunAsync<int>(async ct => { try { await Task.Delay(Timeout.Infinite, ct); return 0; } finally { cleaned = true; } },
            async (_, ct) => { cancel.Cancel(); await Task.Delay(Timeout.Infinite, ct); return "local"; }, _ => {}, cancel.Token, TimeSpan.FromMilliseconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); Assert.True(cleaned);
    }
    [Fact] public async Task FailedRuntimeDownloadPreservesExistingFilesAndVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "runtime"); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "version"), "9.0.0");
        File.WriteAllText(Path.Combine(target, "old.dll"), "working");
        try {
            await Assert.ThrowsAsync<TimeoutException>(() => DotNetRuntimeManager.EnsureRuntimeAsync(new DirectoryInfo(target), "10.0.1", "win-x64", "Runtime",
                packageSource: _ => Task.FromException<string>(new TimeoutException())));
            Assert.Equal("9.0.0", File.ReadAllText(Path.Combine(target, "version")));
            Assert.Equal("working", File.ReadAllText(Path.Combine(target, "old.dll")));
            Assert.Single(Directory.GetDirectories(root));
        } finally { Directory.Delete(root, true); }
    }
    [Fact] public void FailedCommitRestoresOriginalRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "runtime"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "old"), "working");
        try {
            Assert.Throws<DirectoryNotFoundException>(() => XIVLauncher.Common.Util.StagedDirectoryCommit.Replace(Path.Combine(root, "missing"), target));
            Assert.Equal("working", File.ReadAllText(Path.Combine(target, "old")));
            var stage = Path.Combine(root, "stage"); Directory.CreateDirectory(stage); File.WriteAllText(Path.Combine(stage, "new"), "ready");
            XIVLauncher.Common.Util.StagedDirectoryCommit.Replace(stage, target); Assert.True(File.Exists(Path.Combine(target, "new"))); Assert.False(File.Exists(Path.Combine(target, "old")));
        } finally { Directory.Delete(root, true); }
    }
}
