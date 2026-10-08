using Avalonia;
using Avalonia.Controls;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private bool regionAutoSaveReady;
    private Control LanguageApplyRow(ComboBox choice, Action apply)
    {
        var row = new DockPanel { LastChildFill = true };
        var button = Button("应用", apply); button.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(button, Dock.Right); row.Children.Add(button); row.Children.Add(choice);
        return row;
    }
    private void ApplyLauncherLanguage()
    {
        if (!settingsLoaded || operation != null) return;
        try
        {
            settings.Language = Localization.Languages[Math.Clamp(languageChoice.SelectedIndex, 0, 3)].Id;
            settings.Save();
            status.Text = "Launcher language saved. Restart the launcher to apply it.";
            _ = ShowLanguageRestartAsync();
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private async Task ShowLanguageRestartAsync()
    {
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = Localization.Display("语言设置已保存。请重新启动启动器以应用到所有页面。"), TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var dialog = new Window { Title = Localization.Display("应用启动器语言"), Width = 460, SizeToContent = SizeToContent.Height, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = Localization.Display("确认") }; close.Click += (_, _) => dialog.Close(); panel.Children.Add(close);
        await dialog.ShowDialog(this);
    }
    private void ApplyClientLanguage()
    {
        if (!settingsLoaded || operation != null) return;
        try
        {
            settings.RegionProfiles["ffxiv"].ClientLanguage = GameClientLanguage.Choices[Math.Clamp(globalClientLanguage.SelectedIndex, 0, GameClientLanguage.Choices.Length - 1)].Value;
            settings.Save();
            status.Text = "Client language applied; it will be used on the next game launch.";
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private void EnableRegionAutoSave()
    {
        if (regionAutoSaveReady) return;
        regionAutoSaveReady = true;
        cdKeyVersion.SelectionChanged += (_, _) => AutoSaveOfficialPreferences();
        euShopLanguage.SelectionChanged += (_, _) => AutoSaveOfficialPreferences();
        foreach (var box in new[] { gamePath, tcPath, globalPath }) box.LostFocus += (_, _) => SaveClientDirectories();
    }
    private void AutoSaveOfficialPreferences()
    {
        if (!regionAutoSaveReady || restoringWebSettings || operation != null || settings.SelectedRegion != "ffxiv") return;
        try
        {
            SaveInternationalWebSettings(); settings.Save();
            status.Text = "Official website preferences saved.";
            _ = RefreshOfficialAsync();
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private void SaveClientDirectories()
    {
        if (!regionAutoSaveReady || !settingsLoaded || operation != null) return;
        try
        {
            var changed = false;
            foreach (var (region, box) in new[] { ("ffxiv_cn", gamePath), ("ffxiv_tc", tcPath), ("ffxiv", globalPath) })
            {
                var profile = settings.RegionProfiles[region]; var path = box.Text?.Trim() ?? "";
                if (profile.GamePath == path) continue;
                profile.GamePath = path; VersionRecords.InitializeClient(profile); changed = true;
            }
            if (!changed) return;
            settings.Save(); RefreshVersionDisplays(); status.Text = "Client directories saved.";
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
}
