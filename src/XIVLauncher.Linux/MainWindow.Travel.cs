using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private ChinaTravelSession? chinaTravel;
    private void ResetChinaTravel() { chinaTravel?.Dispose(); chinaTravel = null; }
    private ChinaTravelSession GetChinaTravelSession()
    {
        if (settings.SelectedRegion != "ffxiv_cn") throw new InvalidOperationException("DC travel is only available for ffxiv_cn.");
        if (chinaTravel?.Account != settings.Account) { ResetChinaTravel(); chinaTravel = new() { Account = settings.Account }; }
        return chinaTravel!;
    }
    private async Task OpenChinaTravelAsync()
    {
        if (operation != null || settings.SelectedRegion != "ffxiv_cn") return;
        var ready = false;
        await RunOperationAsync(async token =>
        {
            SaveSettings(false); EnsureGamesStopped();
            var session = GetChinaTravelSession();
            using var cancellation = token.Register(() => session.Client.EndSession());
            if (!session.Authenticated)
            {
                var request = new LoginRequest
                {
                    Account = settings.Account, Secret = "",
                    DeviceProfile = settings.GetDeviceProfile(string.IsNullOrWhiteSpace(settings.Account) ? "qr-default" : settings.Account),
                    LoginCancellationTokenSource = operation, LoginSessionRefreshSink = session,
                    ShowQRCode = bytes => Dispatcher.UIThread.Post(() => { qrBitmap?.Dispose(); qrBitmap = new Bitmap(new MemoryStream(bytes)); qr.Source = qrBitmap; qr.IsVisible = true; }),
                    ShowLoginMessage = message => Dispatcher.UIThread.Post(() => AppendLog(message)),
                };
                AppendLog("Sign in to use ffxiv_cn DC travel.");
                var result = await Task.Run(() => ChinaLoginSession.AuthenticateAsync(settings, request,
                    message => Dispatcher.UIThread.Post(() => AppendLog(message)), token), token);
                if (result.State != LoginState.Ok || !session.Authenticated) throw new IOException("DC travel sign-in failed.");
                session.Account = settings.Account; account.Text = settings.Account;
            }
            session.Client.BeginSession();
            await session.Client.GetValidCookie().WaitAsync(token);
            ready = true;
        });
        if (!ready) { ResetChinaTravel(); return; }
        var window = new ChinaTravelWindow(chinaTravel!.Client, EnsureGamesStopped, areaName =>
        {
            settings.AreaName = areaName; settings.Save();
            area.SelectedItem = areas.FirstOrDefault(a => a.AreaName == areaName)?.AreaName;
            if (area.SelectedItem == null) areaInstruction.Text = "超域操作成功，已保存目标大区。请重新打开启动器加载大区列表并确认选择后再启动。";
        }, (message, error) => { AppendLog(message, error == null ? "INFO" : "ERROR", "ffxiv_cn"); if (error != null) AppendLog(DiagnosticLog.Format(error), "DETAIL", "ffxiv_cn"); });
        var startGame = await window.ShowDialog<bool>(this);
        ResetChinaTravel();
        if (startGame) await RunOperationAsync(LaunchAsync, clearLoginInputs: true);
    }
}
