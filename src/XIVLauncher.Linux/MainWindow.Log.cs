using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly TextBox diagnosticPath = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly ScrollViewer gamePromptHost = new() { MaxHeight = 240, Margin = new Thickness(28, 0, 28, 8) };
    private readonly LauncherLog launcherLog = new();
    private readonly SessionDiagnostics sessionDiagnostics = new(LinuxSettings.DataRoot);
    private bool logWriteWarningShown;
    private readonly TextBox logBox = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        Height = 160, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
    private readonly TextBlock credentialInstruction = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock authInstruction = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox globalClientLanguage = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private Control globalClientLanguageField = null!;
    private bool CanLaunch => LinuxSettings.Regions.Any(r => r.Id == settings.SelectedRegion && r.Available);
    private int logRenderPending;
    private void AppendLog(string message, string level = "INFO", string? region = null)
    {
        var target = region ?? settings.SelectedRegion;
        sessionDiagnostics.Write(EnglishLog.Message(message), target, level);
        if (level != "DETAIL") launcherLog.Append(UserLogMessage.Compact(message), target, level);
        if (sessionDiagnostics.WriteError is { } error && !logWriteWarningShown)
        { logWriteWarningShown = true; launcherLog.Append(error, "shared", "WARN"); }
    }
    private void ReportFailure(Exception error)
    {
        if (error is CredentialValidationException validation) { credentialInstruction.Text = validation.UserMessage; tabs.SelectedIndex = 0; Dispatcher.UIThread.Post(() => credentialInstruction.BringIntoView(), DispatcherPriority.Loaded); }
        if (error is Global.GlobalAuthenticationException authentication) { credentialInstruction.Text = authentication.UserMessage; tabs.SelectedIndex = GameTab; Dispatcher.UIThread.Post(() => credentialInstruction.BringIntoView(), DispatcherPriority.Loaded); }
        if (error is XIVLauncher.Login.Exceptions.LoginException china)
        {
            credentialInstruction.Text = Localization.T("中国区登录验证失败。请检查叨鱼中的账号状态后重试；需要扫码时将自动显示二维码。") + $" ({china.ErrorCode})";
            tabs.SelectedIndex = GameTab;
            Dispatcher.UIThread.Post(() => credentialInstruction.BringIntoView(), DispatcherPriority.Loaded);
        }
        AppendLog(UserLogMessage.Failure(error), "ERROR");
        AppendLog(DiagnosticLog.Format(error), "DETAIL");
    }
    private Control WithLogPanel(Control content)
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
        layout.Children.Add(new ScrollViewer { Content = content });
        var prompts = new StackPanel { Spacing = 5 };
        prompts.Children.Add(areaInstruction); prompts.Children.Add(qr); prompts.Children.Add(authInstruction);
        prompts.Children.Add(manualOtpPanel); prompts.Children.Add(credentialInstruction);
        gamePromptHost.Content = prompts; Grid.SetRow(gamePromptHost, 1); layout.Children.Add(gamePromptHost);
        var panel = new StackPanel { Margin = new Thickness(28, 0, 28, 20), Spacing = 6 };
        var header = new DockPanel { LastChildFill = true };
        var clear = Button("清空日志", () => launcherLog.Clear()); DockPanel.SetDock(clear, Dock.Right);
        header.Children.Add(clear); header.Children.Add(new TextBlock { Text = "日志信息（可选择并复制）", VerticalAlignment = VerticalAlignment.Center });
        diagnosticPath.Text = Localization.Display("完整诊断日志：") + sessionDiagnostics.FilePath;
        panel.Children.Add(diagnosticPath); panel.Children.Add(header); panel.Children.Add(logBox); Grid.SetRow(panel, 2); layout.Children.Add(panel);
        launcherLog.Changed += () =>
        {
            if (Interlocked.Exchange(ref logRenderPending, 1) != 0) return;
            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref logRenderPending, 0);
                var start = logBox.SelectionStart; var end = logBox.SelectionEnd;
                var follow = start == end && logBox.CaretIndex == (logBox.Text?.Length ?? 0);
                logBox.Text = launcherLog.Text;
                if (follow) logBox.CaretIndex = logBox.Text.Length;
                else { logBox.SelectionStart = Math.Min(start, logBox.Text.Length); logBox.SelectionEnd = Math.Min(end, logBox.Text.Length); }
            });
        };
        return layout;
    }
}
