using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
namespace XIVLauncher.Linux;
public sealed class LocalOtpWindow : Window
{
    public LocalOtpWindow(string initialSecret = "")
    {
        Title = "本地 2FA 验证码生成器"; Width = 480; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var secret = new TextBox { PasswordChar = '●', Watermark = "Base32 密钥", Text = initialSecret };
        var code = new TextBox { IsReadOnly = true, FontSize = 30 };
        var remaining = new TextBlock();
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "完全本地计算，不发送登录请求、不保存输入。可与手机验证器同时显示的验证码对照。使用当前系统时间（30 秒周期）。", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(secret); panel.Children.Add(code); panel.Children.Add(remaining);
        var copy = new Button { Content = "复制验证码" };
        copy.Click += async (_, _) => { if (OneTimePassword.IsManualCode(code.Text ?? "") && Clipboard != null) await Clipboard.SetTextAsync(code.Text); };
        panel.Children.Add(copy); Content = panel;
        void Refresh()
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                code.Text = OneTimePassword.Generate(secret.Text ?? "", now);
                remaining.Text = $"有效时间剩余 {30 - now.ToUnixTimeSeconds() % 30} 秒 · UTC {now:HH:mm:ss}"; copy.IsEnabled = true;
            }
            catch (ArgumentException) { code.Text = ""; remaining.Text = "请输入有效的 Base32 密钥。"; copy.IsEnabled = false; }
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; timer.Tick += (_, _) => Refresh();
        Opened += (_, _) => { Refresh(); timer.Start(); };
        Closed += (_, _) => { timer.Stop(); secret.Text = ""; code.Text = ""; };
    }
}
