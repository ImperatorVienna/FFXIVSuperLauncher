using System.Net;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Common.Constant;
using XIVLauncher.Common.Util;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Exceptions;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Workflow;

namespace XIVLauncher.Login.Channels;

public sealed class LoginChannelContext
{
    private readonly HttpClient            loginHttpClient;
    private readonly CookieContainer       loginCookies;
    private readonly DeviceProfileSnapshot deviceProfile;
    private readonly string                casCID;

    private int casDomainMode;

    public LoginChannelContext(DeviceProfileSnapshot deviceProfile)
    {
        ArgumentNullException.ThrowIfNull(deviceProfile);

        this.deviceProfile = deviceProfile;
        loginCookies       = new CookieContainer();
        var loginHandler = new HttpClientHandler
        {
            UseCookies      = true,
            CookieContainer = loginCookies
        };

        loginHttpClient = new HttpClient(loginHandler);
        casCID          = deviceProfile.CasCid;
        casDomainMode   = 0;
    }

    public static LoginResult BuildOkLoginResult
    (
        string                 account,
        string                 sid,
        string?                sessionID,
        string?                autoLoginSessionKey,
        LoginType              loginType,
        string?                tgt           = null,
        string?                guid          = null,
        DeviceProfileSnapshot? deviceProfile = null
    )
    {
        var oath = new OAuthLoginResult
        {
            SessionID        = sessionID ?? string.Empty,
            InputUserID      = account,
            SndaID           = sid,
            QuickLoginSecret = autoLoginSessionKey,
            MaxExpansion     = FFXIV.MAX_EXPANSION,
            LoginType        = loginType,
            TGT              = tgt,
            Guid             = guid,
            DeviceProfile    = deviceProfile
        };

        return new LoginResult
        {
            OAuthLogin = oath,
            State      = LoginState.Ok
        };
    }

    public Task<LoginResponse> GetJsonAsync
    (
        string            endPoint,
        List<string>      paras,
        string?           tgt               = null,
        string            appId             = SdoInfos.LAUNCHER_APP_ID,
        CancellationToken cancellationToken = default
    ) =>
        GetJsonAsSdoClient(endPoint, paras, tgt, appId, cancellationToken);

    public async Task<string> GetGuidAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsSdoClient("getGuid.json", ["generateDynamicKey=1"], cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ErrorType != 0)
            throw new Exceptions.OAuthLoginException(result.ToString());

        return result.Data.Guid;
    }

    public async Task<(string SID, string TGT, string AutoLoginSessionKey)> UpdateAutoLoginSessionKeyAsync
    (
        string guid,
        string autoLoginSessionKey,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetJsonAsSdoClient("autoLogin.json", [$"autoLoginSessionKey={Uri.EscapeDataString(autoLoginSessionKey)}", $"guid={guid}"], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ReturnCode != 0)
            throw new LoginException(result.ReturnCode, result.Data.FailReason, true);

        return (result.Data.SndaID, result.Data.Tgt, result.Data.QuickLoginSecret);
    }

    public async Task<string?> GetAccountGroupAsync(string tgt, string sid, CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsSdoClient("getAccountGroup", [$"serviceUrl={Uri.EscapeDataString(Links.SDO_SERVICE_URL)}", $"tgt={tgt}"], cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ReturnCode != 0 || result.ErrorType != 0)
            throw new LoginException(result.ReturnCode, result.Data.FailReason);

        var index = result.Data.SndaIDArray.IndexOf(sid);
        if (index < 0)
            throw new LoginException((int)LoginExceptionCode.ScanQrCodeGetAccountFail, "扫描二维码后获取用户名失败");

        return result.Data.AccountArray[index];
    }

    public async Task<(string TGT, string AutoLoginSessionKey)> AccountGroupLoginAsync
    (
        string tgt,
        string sid,
        int    autoLoginKeepDays,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetJsonAsSdoClient
                     (
                         "accountGroupLogin",
                         [$"serviceUrl={Uri.EscapeDataString(Links.SDO_SERVICE_URL)}", $"tgt={tgt}", $"sndaId={sid}", "autoLoginFlag=1", $"autoLoginKeepTime={autoLoginKeepDays}"],
                         cancellationToken: cancellationToken
                     ).ConfigureAwait(false);

        if (result.ReturnCode != 0)
            throw new LoginException(result.ReturnCode, result.Data.FailReason);

        return (result.Data.Tgt, result.Data.QuickLoginSecret);
    }

    public async Task<(string CodeKey, byte[] QRCode, CancellationTokenSource CTS)> GetQRCodeAsync(int qrCodeExpirationTime, CancellationToken cancellationToken = default)
    {
        using var response = await SendSdoHttpRequestAsync(HttpMethod.Get, "getCodeKey.json", ["maxsize=89"], cancellationToken: cancellationToken).ConfigureAwait(false);
        var cookies  = response.Headers.TryGetValues("Set-Cookie", out var setCookieValues) ? setCookieValues : [];
        var codeKey  = cookies.FirstOrDefault(x => x.StartsWith("CODEKEY=", StringComparison.Ordinal))?.Split(';')[0];
        codeKey = codeKey?.Split('=')[1];
        if (string.IsNullOrEmpty(codeKey))
            throw new Exceptions.OAuthLoginException("QRCode下载失败");

        var qrCodeExpiration = new CancellationTokenSource(qrCodeExpirationTime);

        try
        {
            using var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(qrCodeExpiration.Token, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(linkedCancellationSource.Token).ConfigureAwait(false);
            return (codeKey, bytes, qrCodeExpiration);
        }
        catch
        {
            qrCodeExpiration.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     使用 GAME_APP_ID 从已有 TGT 实时换取 session ticket
    /// </summary>
    public async Task<string> GetSessionIdAsync(string tgt, string guid)
    {
        _ = await GetPromotionInfoAsync(tgt, appId: SdoInfos.APP_ID).ConfigureAwait(false);
        return await SsoLoginAsync(tgt, guid).ConfigureAwait(false);
    }

    public async Task<string> GetDCTravelSessionIDAsync(string tgt, string guid)
    {
        _ = await GetPromotionInfoAsync(tgt, Links.DC_TRAVEL_PAGE_URL).ConfigureAwait(false);
        return await SsoLoginAsync(tgt, guid).ConfigureAwait(false);
    }

    public void BindLoginSessionRefresh(ILoginSessionRefreshSink? loginSessionRefreshSink, string tgt, string guid)
    {
        loginSessionRefreshSink?.Bind
        (
            new LoginSessionRefreshContext
            {
                RefreshDcTravelSessionIdAsync = () => GetDCTravelSessionIDAsync(tgt, guid),
                RefreshGameSessionIdAsync     = () => GetSessionIdAsync(tgt, guid)
            }
        );
    }

    private static LoginResponse DeserializeLoginResponse(string endPoint, string reply)
    {
        try
        {
            var result = JsonConvert.DeserializeObject<LoginResponse>(reply)!;
            Log.Information
            (
                "{EndPoint}:ErrorType={ResultErrorType}:ReturnCode={ResultReturnCode}:FailReason:{DataFailReason}:NextAction={DataNextAction}",
                endPoint,
                result.ErrorType,
                result.ReturnCode,
                result.Data.FailReason,
                result.Data.NextAction
            );
            return result;
        }
        catch (JsonReaderException ex)
        {
            throw new JsonReaderException($"{ex.Message}\n {reply}");
        }
    }

    private async Task<HttpResponseMessage> SendSdoHttpRequestAsync
    (
        HttpMethod            method,
        string                endPoint,
        IReadOnlyList<string> paras,
        string?               tgt               = null,
        string                appId             = SdoInfos.LAUNCHER_APP_ID,
        CancellationToken     cancellationToken = default
    )
    {
        using var request = GetSdoHttpRequestMessage(method, endPoint, paras, tgt, appId);
        return await loginHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage GetSdoHttpRequestMessage
    (
        HttpMethod            method,
        string                endPoint,
        IReadOnlyList<string> paras,
        string?               tgt   = null,
        string                appId = SdoInfos.LAUNCHER_APP_ID
    )
    {
        var request = new HttpRequestMessage(method, BuildSdoRequestUri(endPoint, paras, appId));
        request.Headers.AddWithoutValidation("Cache-Control", "no-cache");
        request.Headers.AddWithoutValidation
        (
            "User-Agent",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 5.1; Trident/4.0; Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1) ; InfoPath.2; .NET CLR 2.0.50727; MS-RTC LM 8; .NET CLR 3.0.04506.648; .NET CLR 3.5.21022; .NET CLR 1.1.4322; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729)"
        );
        request.Headers.AddWithoutValidation("Host", request.RequestUri!.Host);

        if (endPoint is "ssoLogin.json" or "getPromotionInfo.json")
            request.Headers.AddWithoutValidation("Cookie", $"CASTGC={tgt}; CAS_LOGIN_STATE=1");

        var hasCid = loginCookies.GetAllCookies().Any(cookie => string.Equals(cookie.Name, "CASCID", StringComparison.OrdinalIgnoreCase));
        if (!hasCid)
            request.Headers.AddWithoutValidation("Cookie", $"CASCID={casCID}; SECURE_CASCID={casCID};");

        return request;
    }

    private Uri BuildSdoRequestUri(string endPoint, IReadOnlyList<string> paras, string appID)
    {
        var allParas = new List<string>(paras.Count + 20);
        allParas.AddRange(paras);
        allParas.AddRange
        (
            [
                "authenSource=1",
                $"appId={appID}",
                "areaId=1",
                $"appIdSite={appID}",
                "locale=zh_CN",
                "productId=4",
                "frameType=1",
                "endpointOS=1",
                "version=21",
                "customSecurityLevel=2",
                $"deviceId={deviceProfile.DeviceId}",
                "thirdLoginExtern=0",
                $"macId={deviceProfile.MacAddress}",
                "epIp=",
                $"epName={deviceProfile.HostName}",
                "extendInfo=",
                "sdoVersion=",
                "runTimeId=",
                "productVersion=",
                "tag=0"
            ]
        );

        var casDomain   = Volatile.Read(ref casDomainMode) == 0 ? SdoInfos.GLOBAL_CAS_DOMAIN : SdoInfos.FALLBACK_CAS_DOMAIN;
        var requestPath = endPoint.StartsWith('/') ? endPoint : $"/authen/{endPoint}";
        var queryString = string.Join("&", allParas);

        return new UriBuilder(Uri.UriSchemeHttps, casDomain)
        {
            Path  = requestPath,
            Query = queryString
        }.Uri;
    }

    private static Uri BuildWebLoginUri(Uri serviceUri, string ticket)
    {
        var uriBuilder      = new UriBuilder(serviceUri);
        var ticketParameter = $"ticket={Uri.EscapeDataString(ticket)}";
        uriBuilder.Query = string.IsNullOrEmpty(uriBuilder.Query)
                               ? ticketParameter
                               : $"{uriBuilder.Query.TrimStart('?')}&{ticketParameter}";
        return uriBuilder.Uri;
    }

    private async Task<LoginResponse> GetJsonAsSdoClient
    (
        string                endPoint,
        IReadOnlyList<string> paras,
        string?               tgt               = null,
        string                appId             = SdoInfos.LAUNCHER_APP_ID,
        CancellationToken     cancellationToken = default
    )
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var response = await SendSdoHttpRequestAsync(HttpMethod.Get, endPoint, paras, tgt, appId, cancellationToken).ConfigureAwait(false);
                var       reply    = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return DeserializeLoginResponse(endPoint, reply);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt == 0 && TrySwitchToFallbackDomain(ex))
            {
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw lastException ?? new InvalidOperationException("Failed to request SDO login endpoint");
    }

    private bool TrySwitchToFallbackDomain(Exception ex)
    {
        if (Interlocked.CompareExchange(ref casDomainMode, 1, 0) != 0)
            return false;

        Log.Error(ex, "[LoginChannelContext] GetJsonAsSdoClient 发生异常，切换备用域名");
        return true;
    }

    private async Task<string> SsoLoginAsync(string tgt, string guid, string appId = SdoInfos.APP_ID)
    {
        var result = await GetJsonAsSdoClient("ssoLogin.json", [$"tgt={tgt}", $"guid={guid}"], tgt, appId).ConfigureAwait(false);
        if (result.ReturnCode != 0)
            throw new LoginException(result.ReturnCode, result.Data.FailReason);

        return result.Data.Ticket;
    }

    private async Task<LoginResponse> GetPromotionInfoAsync(string tgt, string? serviceUrl = null, string appId = SdoInfos.APP_ID)
    {
        var paras = new List<string> { $"tgt={tgt}" };
        if (serviceUrl != null)
            paras.Add($"serviceUrl={Uri.EscapeDataString(serviceUrl)}");

        var result = await GetJsonAsSdoClient("getPromotionInfo.json", paras, tgt, appId).ConfigureAwait(false);
        if (result.ReturnCode != 0)
            throw new LoginException(result.ReturnCode, result.Data.FailReason);

        return result;
    }
}
