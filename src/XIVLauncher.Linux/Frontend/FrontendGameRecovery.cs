using System.Text.Json;
using XIVLauncher.Linux.ChinaTravel;

namespace XIVLauncher.Linux.Frontend;

// A reopened UI has its own command host. Only authentication is forwarded to the
// original game host; game credentials never enter the renderer or the new host.
internal sealed class FrontendGameRecovery(string root, HttpClient? transport = null)
{
    private GameRecoveryEndpoint.Discovery? selected;
    internal async Task<object> CheckAsync(CancellationToken token)
    {
        selected = await new GameSessionDiscovery(root, transport).FindAsync(token);
        return new { required = selected != null };
    }
    internal object Start(FrontendOperations operations)
    {
        var target = selected ?? throw new InvalidOperationException("No game session needs authentication.");
        return operations.Start("ffxiv_cn", async operation =>
        {
            var completed = false;
            try
            {
                operation.Status("正在恢复运行中游戏的跨区授权；无需退出游戏。");
                var state = await SendAsync(target, "start", operation.Token);
                string? image = null;
                while (state.Status == "scanning")
                {
                    if (state.Image != null && state.Image != image)
                    {
                        image = state.Image;
                        operation.Notify("qr", "请使用当前游戏账号扫码，恢复跨区授权。", image);
                    }
                    await Task.Delay(500, operation.Token);
                    state = await SendAsync(target, "state", operation.Token);
                }
                operation.ClearNotification();
                if (state.Status != "ready") throw new CredentialValidationException("Game session authentication failed.", state.Message ?? "认证未完成，请重新打开启动器重试。");
                completed = true;
                operation.Status("跨区授权已恢复，请回到游戏重新点击跨区；本次不会自动提交订单。");
            }
            finally
            {
                if (!completed)
                    try { await SendAsync(target, "cancel", CancellationToken.None); } catch (HttpRequestException) { } catch (TaskCanceledException) { } catch (JsonException) { } catch (IOException) { }
            }
        });
    }
    private Task<RecoveryState> SendAsync(GameRecoveryEndpoint.Discovery target, string command, CancellationToken token) =>
        GameRecoveryClient.SendAsync(target, command, token, transport);
}
