using XIVLauncher.Login.Client;
using XIVLauncher.Login.Workflow;

namespace XIVLauncher.Linux.Frontend;

internal static class FrontendLoginRequest
{
    internal static LoginRequest Create(LinuxSettings settings, FrontendOperation operation, string password = "", ILoginSessionRefreshSink? session = null) => new()
    {
        Account = settings.Account, Secret = password, LoginCancellationTokenSource = operation.Cancellation,
        DeviceProfile = settings.SelectedRegion == "ffxiv_cn" ? settings.GetDeviceProfile(settings.Account) : XIVLauncher.Account.DeviceProfiles.FakeMachineInfo.CreateSnapshot(),
        LoginSessionRefreshSink = session,
        ShowQRCode = bytes => operation.Notify("qr", "请使用叨鱼扫描二维码登录。", "data:image/png;base64," + Convert.ToBase64String(bytes)),
        ShowLoginMessage = message => operation.Notify("notice", message),
        ShowVerificationCode = operation.NotifyMobile
    };
}
