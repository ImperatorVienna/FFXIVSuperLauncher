using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly StackPanel manualOtpPanel = new() { Spacing = 8, IsVisible = false };
    private async Task<string> RequestManualOtpAsync(CancellationToken token)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return await Dispatcher.UIThread.InvokeAsync(() => RequestManualOtpAsync(token));
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new TextBox { Watermark = settings.SelectedRegion == "ffxiv" ? "2FA 验证码（可选）" : "请输入当前六位 2FA 验证码", MaxLength = 6, PasswordChar = '●' };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var submit = Button("确认并登录", () =>
        {
            var code = input.Text?.Trim() ?? "";
            if (!OneTimePassword.IsManualCode(code)) { feedback.Text = "请输入六位数字验证码。"; return; }
            completion.TrySetResult(code);
        });
        var skip = Button("账号未启用 2FA，继续", () => completion.TrySetResult(""));
        manualOtpPanel.Children.Clear();
        manualOtpPanel.Children.Add(new TextBlock { Text = settings.SelectedRegion == "ffxiv_tc" ? "准备完成。请输入当前验证码后立即提交登录；请勿关闭网页验证窗口。" : "准备完成。请输入当前验证码后提交登录；未启用 2FA 的账号可选择继续。", TextWrapping = TextWrapping.Wrap });
        manualOtpPanel.Children.Add(input); manualOtpPanel.Children.Add(feedback);
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 }; actions.Children.Add(submit); actions.Children.Add(skip); manualOtpPanel.Children.Add(actions);
        manualOtpPanel.IsVisible = true; tabs.SelectedIndex = 0; Activate(); input.Focus(); Dispatcher.UIThread.Post(() => manualOtpPanel.BringIntoView(), DispatcherPriority.Loaded);
        using var registration = token.Register(() => completion.TrySetCanceled(token));
        try { return await completion.Task.WaitAsync(TimeSpan.FromSeconds(90), token); }
        catch (TimeoutException) { throw new TimeoutException("Verification input timed out. Start sign-in again to obtain fresh verification tokens."); }
        finally { input.Text = ""; manualOtpPanel.IsVisible = false; manualOtpPanel.Children.Clear(); }
    }
}
