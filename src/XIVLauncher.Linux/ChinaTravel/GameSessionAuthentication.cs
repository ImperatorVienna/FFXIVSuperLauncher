using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Login.Channels;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Exceptions;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Workflow;

namespace XIVLauncher.Linux.ChinaTravel;

internal sealed class GameSessionLoginRequiredException() : Exception("跨区登录授权已失效，请打开启动器扫码恢复；无需退出游戏。" );
internal sealed record RecoveryState(string Status, string? Image = null, string? Message = null);

// Owned by the original game backend, never by the temporary Electron window.
internal sealed class GameSessionAuthentication : IAsyncDisposable
{
    private readonly string root, account, sndaId;
    private readonly DeviceProfileSnapshot device;
    private readonly Func<LoginType, LoginRequest, CancellationToken, Task<LoginResult>> login;
    private readonly Func<OAuthLoginResult, LoginSessionRefreshContext> makeRefresh;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private LoginSessionRefreshContext current;
    private RecoveryState state = new("ready");
    private CancellationTokenSource? scanCancellation;
    private Task scanTask = Task.CompletedTask;
    private int disposed;
    public RecoveryState State { get { lock (sync) return state; } }
    public LoginSessionRefreshContext Refresh { get; }

    internal GameSessionAuthentication(string root, string account, string sndaId, DeviceProfileSnapshot device,
        LoginSessionRefreshContext initial,
        Func<LoginType, LoginRequest, CancellationToken, Task<LoginResult>>? login = null,
        Func<OAuthLoginResult, LoginSessionRefreshContext>? makeRefresh = null)
    {
        this.root = root; this.account = account; this.sndaId = sndaId; this.device = device;
        current = initial; this.login = login ?? new LoginClient().LoginAsync;
        this.makeRefresh = makeRefresh ?? (oauth => new LoginSessionRefreshContext
        {
            RefreshGameSessionIdAsync = () => new LoginChannelContext(device).GetSessionIdAsync(oauth.TGT!, oauth.Guid!),
            RefreshDcTravelSessionIdAsync = () => new LoginChannelContext(device).GetDCTravelSessionIDAsync(oauth.TGT!, oauth.Guid!)
        });
        Refresh = new() { RefreshGameSessionIdAsync = () => RefreshAsync(false), RefreshDcTravelSessionIdAsync = () => RefreshAsync(true) };
    }

    private void SetState(RecoveryState value) { lock (sync) state = value; }
    private async Task<string> RefreshAsync(bool travel)
    {
        await gate.WaitAsync(lifetime.Token);
        try
        {
            if (State.Status != "ready") throw new GameSessionLoginRequiredException();
            try { return await Invoke(); }
            catch (LoginException ex) when (ex.ErrorCode == (int)LoginExceptionCode.OutdatedLoginInfo)
            {
                var secret = await RegionCredentials.ReadAsync(root, "ffxiv_cn", account, "session", lifetime.Token);
                if (!string.IsNullOrEmpty(secret))
                {
                    try
                    {
                        var result = await login(LoginType.QuickLogin, Request(secret, lifetime), lifetime.Token);
                        await AcceptAsync(result, lifetime.Token);
                        return await Invoke();
                    }
                    catch (LoginException rejected) when (rejected.RemoveQuickLoginSecret || rejected.ErrorCode == (int)LoginExceptionCode.OutdatedLoginInfo)
                    {
                        await RegionCredentials.RemoveValueAsync(root, "ffxiv_cn", account, "session", lifetime.Token);
                    }
                }
                SetState(new("required"));
                throw new GameSessionLoginRequiredException();
            }
        }
        finally { gate.Release(); }
        Task<string> Invoke() => (travel ? current.RefreshDcTravelSessionIdAsync() : current.RefreshGameSessionIdAsync()).WaitAsync(lifetime.Token);
    }

    private LoginRequest Request(string secret, CancellationTokenSource cancellation, Action<byte[]>? show = null) => new()
    {
        Account = account, Secret = secret, QuickLoginEnabled = true, DeviceProfile = device,
        LoginCancellationTokenSource = cancellation, ShowQRCode = show
    };

    private async Task AcceptAsync(LoginResult result, CancellationToken token)
    {
        var oauth = result.OAuthLogin;
        if (result.State != LoginState.Ok || oauth == null || oauth.SndaID != sndaId ||
            string.IsNullOrEmpty(oauth.TGT) || string.IsNullOrEmpty(oauth.Guid))
            throw new LoginException((int)LoginExceptionCode.OutdatedLoginInfo, "请使用当前游戏账号扫码。" );
        // Reject a different account before changing credentials or refresh callbacks.
        if (!string.IsNullOrEmpty(oauth.QuickLoginSecret))
            await RegionCredentials.StoreValueAsync(root, "ffxiv_cn", account, "session", oauth.QuickLoginSecret, token);
        current = makeRefresh(oauth);
        SetState(new("ready"));
    }

    public RecoveryState StartScan()
    {
        lock (sync)
        {
            if (state.Status == "ready" || !scanTask.IsCompleted) return state;
            scanCancellation?.Dispose();
            scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            scanCancellation.CancelAfter(TimeSpan.FromMinutes(5));
            state = new("scanning");
            var cancellation = scanCancellation;
            scanTask = Task.Run(() => ScanAsync(cancellation));
            return state;
        }
    }

    private async Task ScanAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await gate.WaitAsync(cancellation.Token);
            try
            {
                var result = await login(LoginType.QRCode, Request("", cancellation,
                    bytes => SetState(new("scanning", "data:image/png;base64," + Convert.ToBase64String(bytes)))), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                await AcceptAsync(result, cancellation.Token);
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) { SetState(new("required", Message: "认证已取消或超时，请重试。")); }
        catch (LoginException) { SetState(new("required", Message: "认证未完成，请使用当前游戏账号重新扫码。")); }
        catch (Exception) { SetState(new("required", Message: "认证服务暂时不可用，请稍后重试。")); }
    }

    public void CancelScan() { lock (sync) scanCancellation?.Cancel(); }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel(); CancelScan();
        await scanTask;
        scanCancellation?.Dispose();
        lifetime.Dispose();
    }
}
