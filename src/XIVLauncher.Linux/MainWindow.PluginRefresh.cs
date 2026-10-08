using Avalonia.Controls;
using Avalonia.Layout;
namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private async Task RefreshPluginStatesAsync()
    {
        if (!settingsLoaded || operation != null) return;
        if (HasPendingPluginChanges())
        {
            var body = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
            var dialog = new Window { Title = "刷新插件启用状态", Width = 440, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = body };
            body.Children.Add(new TextBlock { Text = "刷新将放弃尚未应用的插件修改，是否继续？", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            body.Children.Add(Button("刷新插件启用状态", () => dialog.Close(true)));
            body.Children.Add(Button("取消", () => dialog.Close(false)));
            if (!await dialog.ShowDialog<bool>(this)) return;
        }
        RefreshPlugins();
    }
}
