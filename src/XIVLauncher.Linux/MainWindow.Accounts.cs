using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private AccountManagerPanel credentialManager = null!;
    private readonly Button chooseAccount = new() { Content = "选择登录名" };
    private async Task ChooseAccountAsync()
    {
        if (!settingsLoaded || operation != null || credentialManager.Busy) return;
        var selectedRegion = settings.SelectedRegion;
        var names = settings.Current.Accounts.Keys.Order().ToArray();
        var list = new ListBox { ItemsSource = names, SelectedItem = settings.Account, MinHeight = 160 };
        var body = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
        var window = new Window { Title = Localization.Display(LinuxSettings.Regions.Single(x => x.Id == selectedRegion).Name) + " · " + Localization.Display("登录名"), Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = body };
        body.Children.Add(new TextBlock { Text = names.Length == 0 ? "此游戏区服还没有保存的账号，请到“登录凭据管理”添加；中国区也可填写登录名后扫码。" : "选择当前游戏区服已保存的登录名。", TextWrapping = TextWrapping.Wrap });
        body.Children.Add(list);
        body.Children.Add(Button("选择", () => { if (list.SelectedItem is string value) window.Close(value); }));
        body.Children.Add(Button("取消", () => window.Close()));
        if (await window.ShowDialog<string?>(this) is not { } selected) return;
        SaveSettings(false); AccountProfiles.Select(settings.Current,selected); settings.Save(); ResetChinaTravel(); UpdateLoginRegion(); AppendLog("Saved account selected.");
    }
    private void ApplyTypedChinaAccount()
    {
        if (settings.SelectedRegion != "ffxiv_cn") return;
        var name = account.Text?.Trim() ?? "";
        if (name == settings.Account) return;
        AccountProfiles.Select(settings.Current, name);
        area.SelectedItem = areas.FirstOrDefault(x => x.AreaName == settings.AreaName)?.AreaName;
        ResetChinaTravel();
    }
    private bool GuideUnknownAccount()
    {
        var value = account.Text?.Trim() ?? "";
        if (settings.SelectedRegion == "ffxiv_cn" || value.Length == 0 || credentialManager.Busy) return false;
        var saved = LinuxSettings.Load(settings.Root);
        if (saved.RegionProfiles[settings.SelectedRegion].Accounts.ContainsKey(value)) return false;
        GuideMissingCredentials(value);
        return true;
    }
    private void GuideMissingCredentials(string? name = null)
    {
        credentialManager.GuideAccount(settings.SelectedRegion, name ?? settings.Account);
        tabs.SelectedIndex = 1;
        credentialInstruction.Text = "这是首次登录此账户，请填写并保存凭据，然后手动回到游戏页选择自己的登录名并登录。";
    }
    private void CredentialsSaved(bool success)
    {
        settings = LinuxSettings.Load(settings.Root); ResetChinaTravel(); UpdateLoginRegion(); AppendLog(success ? "Credential changes saved." : "Credential save failed; check the credential manager for details.", success ? "INFO" : "ERROR");
    }
}
