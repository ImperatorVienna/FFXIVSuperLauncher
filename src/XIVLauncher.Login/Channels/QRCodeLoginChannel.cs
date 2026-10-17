using XIVLauncher.Login.Client;
using XIVLauncher.Login.Exceptions;
using XIVLauncher.Login.Models;

namespace XIVLauncher.Login.Channels;

public sealed class QRCodeLoginChannel
(
    LoginChannelContext context
) : ILoginChannel
{
    public        LoginType Type => LoginType.QRCode;
    private const int       QR_CODE_EXPIRATION_TIME = 300 * 1000;
    private const int       AUTO_LOGIN_KEEP_DAYS    = 30;

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request.LoginCancellationTokenSource == null)
            throw new LoginException((int)LoginExceptionCode.ScanTimeoutOrCanceled, "登录取消令牌不能为空");

        using var loginCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.LoginCancellationTokenSource.Token);
        cancellationToken = loginCancellation.Token;
        cancellationToken.ThrowIfCancellationRequested();

        var     guid    = await context.GetGuidAsync(cancellationToken).ConfigureAwait(false);
        string? sndaID  = null;
        string? tgt     = null;
        string? account = null;

        // Renew an expired QR once. A failed scan must not turn into an unbounded polling loop.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var qrCodeResult = await context.GetQRCodeAsync(QR_CODE_EXPIRATION_TIME, cancellationToken).ConfigureAwait(false);
            using var expiration = qrCodeResult.CTS;
            request.ShowQRCode?.Invoke(qrCodeResult.QRCode);
            (sndaID, tgt, account) = await WaitForScanAsync(qrCodeResult.CodeKey, guid, expiration, cancellationToken).ConfigureAwait(false);
            if (sndaID != null)
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (sndaID == null)
            throw new LoginException((int)LoginExceptionCode.ScanTimeoutOrCanceled, "二维码不存在或已过期，请重试");
        var newAccount = await context.GetAccountGroupAsync(tgt!, sndaID!, cancellationToken).ConfigureAwait(false);
        account = string.IsNullOrEmpty(account) ? newAccount : account;
        string? autoLoginSessionKey = null;
        if (request.QuickLoginEnabled)
            (tgt, autoLoginSessionKey) = await context.AccountGroupLoginAsync(tgt!, sndaID!, AUTO_LOGIN_KEEP_DAYS, cancellationToken).ConfigureAwait(false);

        context.BindLoginSessionRefresh(request.LoginSessionRefreshSink, tgt!, guid);
        return LoginChannelContext.BuildOkLoginResult(account!, sndaID!, null, request.QuickLoginEnabled ? autoLoginSessionKey : null, LoginType.QRCode, tgt, guid, request.DeviceProfile);
    }

    internal async Task<(string? sndaId, string? tgt, string? account)> WaitForScanAsync(string codeKey, string guid, CancellationTokenSource qrCodeExpiration, CancellationToken userCancel)
    {
        using var polling = CancellationTokenSource.CreateLinkedTokenSource(qrCodeExpiration.Token, userCancel);
        try
        {
            while (true)
            {
                polling.Token.ThrowIfCancellationRequested();
                var result = await context.GetJsonAsync
                (
                    "codeKeyLogin.json",
                    [$"codeKey={codeKey}", $"guid={guid}", "autoLoginFlag=1", $"autoLoginKeepTime={AUTO_LOGIN_KEEP_DAYS}", "maxsize=97"],
                    cancellationToken: polling.Token
                ).ConfigureAwait(false);

                if (result.ReturnCode == 0 && result.Data.NextAction == 0)
                    return (result.Data.SndaID, result.Data.Tgt, result.Data.InputUserID);

                // Check expiry before the pending-scan code: the service may return both.
                if (result.Data.FailReason == "二维码不存在或已过期，请重试")
                    return (null, null, null);

                if (result.ReturnCode == (int)LoginExceptionCode.QrCodeVerifyFailed)
                {
                    await Task.Delay(1000, polling.Token).ConfigureAwait(false);
                    continue;
                }

                throw new OAuthLoginException(result.Data.FailReason);
            }
        }
        catch (OperationCanceledException) when (qrCodeExpiration.IsCancellationRequested && !userCancel.IsCancellationRequested)
        {
            return (null, null, null);
        }
    }
}
