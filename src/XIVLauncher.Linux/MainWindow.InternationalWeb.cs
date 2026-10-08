using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly ComboBox cdKeyVersion = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox euShopLanguage = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Grid internationalWebRow = new() { ColumnDefinitions = new ColumnDefinitions("*,*"), IsVisible = false };
    private Control euShopLanguageField = null!;
    private void InitializeInternationalWebSettings()
    {
        cdKeyVersion.ItemsSource = InternationalWeb.CdKeyVersions; cdKeyVersion.SelectedIndex = 1;
        euShopLanguage.ItemsSource = InternationalWeb.EuLanguages.Select(x => x.Name).ToArray(); euShopLanguage.SelectedIndex = 0;
        internationalWebRow.Children.Add(Field("账户CDKey版本", cdKeyVersion));
        euShopLanguageField = Field("官网及商城偏好语言", euShopLanguage);
        euShopLanguageField.Margin = new Thickness(12, 0, 0, 0); euShopLanguageField.IsVisible = false;
        Grid.SetColumn(euShopLanguageField, 1); internationalWebRow.Children.Add(euShopLanguageField);
        regionPanel.Children.Add(internationalWebRow);
        cdKeyVersion.SelectionChanged += (_, _) => euShopLanguageField.IsVisible = cdKeyVersion.SelectedItem as string == "EU";
    }
    private void SaveInternationalWebSettings()
    {
        settings.Current.CdKeyVersion = cdKeyVersion.SelectedItem as string ?? "NA";
        settings.Current.EuShopLanguage = InternationalWeb.EuLanguages[Math.Clamp(euShopLanguage.SelectedIndex, 0, InternationalWeb.EuLanguages.Length - 1)].Value;
    }
    private bool restoringWebSettings;
    private void RestoreInternationalWebSettings()
    {
        restoringWebSettings = true;
        try
        {
        internationalWebRow.IsVisible = settings.SelectedRegion == "ffxiv";
        var profile = settings.RegionProfiles["ffxiv"];
        var index = Array.IndexOf(InternationalWeb.CdKeyVersions, profile.CdKeyVersion);
        cdKeyVersion.SelectedIndex = index < 0 ? 1 : index;
        euShopLanguage.SelectedIndex = Math.Max(0, Array.FindIndex(InternationalWeb.EuLanguages, x => x.Value == profile.EuShopLanguage));
        euShopLanguageField.IsVisible = cdKeyVersion.SelectedItem as string == "EU";
        }
        finally { restoringWebSettings = false; }
    }
}
