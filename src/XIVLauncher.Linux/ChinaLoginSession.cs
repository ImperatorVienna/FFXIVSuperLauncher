using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Exceptions;
namespace XIVLauncher.Linux;

/// <summary>Uses Soil's rotating quick-login secret; never persists one-use game session tickets.</summary>
public static class ChinaLoginSession
{
    public static async Task<LoginResult> AuthenticateAsync(LinuxSettings settings, LoginRequest request,
        Action<string> status, CancellationToken token,
        Func<LoginType, LoginRequest, CancellationToken, Task<LoginResult>>? login = null)
    {
        if (settings.SelectedRegion != "ffxiv_cn") throw new InvalidOperationException("Quick login is only available for ffxiv_cn.");
        login ??= new LoginClient().LoginAsync;
        var remember = settings.Current.RememberPassword;
        var secret = remember && !string.IsNullOrWhiteSpace(request.Account)
            ? await RegionCredentials.ReadAsync(settings, request.Account, "session", token) : null;
        LoginRequest Request(string account, string credential) => LoginRequest.Create(account, credential, remember, request.DeviceProfile,
            request.LoginSessionRefreshSink, request.LoginCancellationTokenSource, request.ShowQRCode,
            request.ShowLoginMessage);
        LoginResult? result = null;
        if (!string.IsNullOrEmpty(secret))
        {
            status("Signing in with saved credentials.");
            try { result = await login(LoginType.QuickLogin, Request(request.Account, secret), token); }
            catch (LoginException ex) when (ex.RemoveQuickLoginSecret && !token.IsCancellationRequested)
            {
                await RegionCredentials.RemoveAsync(settings, request.Account, "session", token);
                status("Saved sign-in expired. Please authenticate again.");
            }
        }
        token.ThrowIfCancellationRequested();
        result ??= await login(LoginType.QRCode, Request(request.Account, ""), token);
        if (result.State == LoginState.Ok && result.OAuthLogin is { } oauth && remember)
        {
            var account = string.IsNullOrWhiteSpace(oauth.InputUserID) ? request.Account : oauth.InputUserID;
            if (string.IsNullOrWhiteSpace(account)) throw new IOException("Sign-in succeeded but the account identifier is missing.");
            // A scanned account may differ from the typed alias. Preserve the profile used to obtain its secret.
            settings.Current.DeviceProfiles[account] = request.DeviceProfile;
            settings.Account = account;
            settings.Save();
            if (!string.IsNullOrWhiteSpace(oauth.QuickLoginSecret))
            {
                await RegionCredentials.StoreAsync(settings, account, "session", oauth.QuickLoginSecret, token);
                status("Sign-in credentials saved for future launches.");
            }
            else status("Sign-in succeeded, but no reusable credential was returned.");
        }
        return result;
    }
}
