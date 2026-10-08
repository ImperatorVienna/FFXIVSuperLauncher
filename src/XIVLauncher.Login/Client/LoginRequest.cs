using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Workflow;

namespace XIVLauncher.Login.Client;

public sealed class LoginRequest
{
    public string                                 Account                      { get; init; } = string.Empty;
    public string                                 Secret                       { get; init; } = string.Empty;
    public bool                                   QuickLoginEnabled            { get; init; }
    public DeviceProfileSnapshot                  DeviceProfile                { get; init; } = FakeMachineInfo.CreateSnapshot();
    public CancellationTokenSource?               LoginCancellationTokenSource { get; init; }
    public Action<byte[]>?                        ShowQRCode                   { get; init; }
    public Action<string>?                        ShowLoginMessage             { get; init; }
    public ILoginSessionRefreshSink?              LoginSessionRefreshSink      { get; init; }

    public static LoginRequest Create
    (
        string                                 account,
        string                                 secret,
        bool                                   quickLoginEnabled,
        DeviceProfileSnapshot                  deviceProfile,
        ILoginSessionRefreshSink?              loginSessionRefreshSink,
        CancellationTokenSource?               loginCancellationTokenSource,
        Action<byte[]>?                        showQRCode,
        Action<string>?                        showLoginMessage
    ) =>
        new()
        {
            Account                      = account,
            Secret                       = secret,
            QuickLoginEnabled            = quickLoginEnabled,
            DeviceProfile                = deviceProfile,
            LoginSessionRefreshSink      = loginSessionRefreshSink,
            LoginCancellationTokenSource = loginCancellationTokenSource,
            ShowQRCode                   = showQRCode,
            ShowLoginMessage             = showLoginMessage
        };
}
