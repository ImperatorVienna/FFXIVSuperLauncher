using System.Net;
using System.Text;
using XIVLauncher.Linux.Platforms.Windows;
using Xunit;

namespace XIVLauncher.Windows.Tests;

public sealed class WindowsLocalHttpTests
{
    [Fact]
    public async Task DisconnectedClientDoesNotStopFollowingRequests()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var bridge = new XIVLauncher.Linux.ChinaTravel.DcTravelerBridge(async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1) { entered.SetResult(); await resume.Task; }
            return "ok";
        });
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var cancellation = new CancellationTokenSource();
        var url = $"http://127.0.0.1:{bridge.Port}/dctravel/";
        static StringContent Body() => new("{\"Method\":\"QueryGroupListTravelSource\",\"Params\":[]}", Encoding.UTF8, "application/json");
        var pending = http.PostAsync(url, Body(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await pending; });
        resume.SetResult();
        using var next = await http.PostAsync(url, Body());
        next.EnsureSuccessStatusCode();
        Assert.Contains("ok", await next.Content.ReadAsStringAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RoutesSharePortAndReleaseIndependently()
    {
        var travel = WindowsLocalHttpListener.Listen(0, "/dctravel/");
        var recovery = WindowsLocalHttpListener.Listen(travel.Port, "/game-auth/");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            var pending = http.PostAsync($"http://127.0.0.1:{travel.Port}/game-auth/state", null);
            var request = await recovery.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("/game-auth/state", request.Path);
            request.ContentType = "application/json";
            await request.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{}")); request.Close();
            using var result = await pending; Assert.Equal("{}", await result.Content.ReadAsStringAsync());
            recovery.Close();
            using var gone = await http.PostAsync($"http://127.0.0.1:{travel.Port}/game-auth/state", null);
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
            var again = http.PostAsync($"http://127.0.0.1:{travel.Port}/dctravel/", null);
            var next = await travel.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(3)); next.Close();
            using var response = await again; response.EnsureSuccessStatusCode();
        }
        finally { recovery.Close(); travel.Close(); }
        var replacement = WindowsLocalHttpListener.Listen(travel.Port, "/dctravel/"); replacement.Close();
    }

    [Fact]
    public async Task ClosingWithAnUnansweredRequestReleasesThePort()
    {
        var listener = WindowsLocalHttpListener.Listen(0, "/dctravel/");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var pending = http.PostAsync($"http://127.0.0.1:{listener.Port}/dctravel/", null);
        try
        {
            var request = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(3));
            listener.Close(); request.Close();
            await Assert.ThrowsAsync<HttpRequestException>(async () => { using var response = await pending; });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.GetContextAsync());
        }
        finally { listener.Close(); }
        var replacement = WindowsLocalHttpListener.Listen(listener.Port, "/dctravel/"); replacement.Close();
    }
}
