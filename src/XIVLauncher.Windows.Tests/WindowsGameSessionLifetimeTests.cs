using System.Diagnostics;
using XIVLauncher.Dalamud;
using XIVLauncher.Linux;
using XIVLauncher.Linux.Platforms;
using XIVLauncher.Linux.Platforms.Windows;
using Xunit;

namespace XIVLauncher.Windows.Tests;

public sealed class WindowsGameSessionLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisconnectRetainsListenerEvenIfProcessWaitCompletesEarly(bool waitThrows)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var bridge = new XIVLauncher.Linux.ChinaTravel.DcTravelerBridge((_, _) => Task.FromResult<object?>("still serving"));
        var game = new Game { ThrowOnWait = waitThrows };
        try
        {
            await WindowsGameSessionLifetime.VerifyAsync(root, [bridge.Port], "before frontend release");
            var waiting = WindowsGameSessionLifetime.WaitAsync(root, game, [bridge.Port]);
            await game.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(waiting.IsCompleted);
            await WindowsLocalHttpListener.VerifyAsync(bridge.Port);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var body = new StringContent("{\"Method\":\"QueryGroupListTravelSource\",\"Params\":[]}", System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync($"http://127.0.0.1:{bridge.Port}/dctravel/", body);
            response.EnsureSuccessStatusCode();
            Assert.Contains("still serving", await response.Content.ReadAsStringAsync());
            game.Running = false;
            await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { game.Running = false; if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ClosedListenerBlocksReleaseButDoesNotEndGameWait()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var listener = WindowsLocalHttpListener.Listen(0, "/dctravel/");
        var game = new Game();
        listener.Close();
        try
        {
            await Assert.ThrowsAsync<IOException>(() => WindowsGameSessionLifetime.VerifyAsync(root, [listener.Port], "before frontend release"));
            var waiting = WindowsGameSessionLifetime.WaitAsync(root, game, [listener.Port]);
            await game.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(waiting.IsCompleted);
            game.Running = false;
            await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { game.Running = false; if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Game : IGameRuntime
    {
        internal volatile bool Running = true;
        internal bool ThrowOnWait;
        internal readonly TaskCompletionSource WaitEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsGameRunning => Running;
        public bool ObserveGame() => Running;
        public Task WaitForExitAsync() { WaitEntered.TrySetResult(); return ThrowOnWait ? Task.FromException(new IOException()) : Task.CompletedTask; }
        public void Validate() => throw new NotSupportedException();
        public Task DiagnoseAsync(Action<string> status, CancellationToken token) => throw new NotSupportedException();
        public RegionDalamudFiles? WithoutDalamud(string region) => throw new NotSupportedException();
        public Task<ProcessStartInfo> PrepareAsync(LinuxSettings settings, RegionDalamudFiles? files, FileInfo game, DalamudStartInfo? info, bool noPlugins, CancellationToken token) => throw new NotSupportedException();
        public Task LaunchAsync(LinuxSettings settings, ProcessStartInfo prepared, string arguments, bool injected, Action<string> status, CancellationToken token) => throw new NotSupportedException();
    }
}
