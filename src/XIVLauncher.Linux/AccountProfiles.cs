namespace XIVLauncher.Linux;

public sealed class SavedAccountSettings
{
    public string AreaName { get; set; } = "";
    public bool RememberPassword { get; set; }
    public bool AutoOtp { get; set; }
    public bool SteamAccount { get; set; }
    public bool FreeTrial { get; set; }
    private string cdKeyVersion = "NA";
    public string CdKeyVersion { get => cdKeyVersion; set => cdKeyVersion = InternationalWeb.NormalizeCdKeyVersion(value); }
    public string EuShopLanguage { get; set; } = "en-gb";
}
public static class AccountProfiles
{
    public static void Capture(RegionSettings profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Account)) return;
        if (!profile.Accounts.TryGetValue(profile.Account, out var saved)) profile.Accounts[profile.Account] = saved = new();
        saved.AreaName = profile.AreaName;
        saved.RememberPassword = profile.RememberPassword; saved.AutoOtp = profile.AutoOtp; saved.SteamAccount = profile.SteamAccount; saved.FreeTrial = profile.FreeTrial;
        saved.CdKeyVersion = profile.CdKeyVersion; saved.EuShopLanguage = profile.EuShopLanguage;
    }
    public static void Select(RegionSettings profile, string account)
    {
        Capture(profile);
        var saved = profile.Accounts.GetValueOrDefault(account) ?? new SavedAccountSettings();
        profile.Account = account; profile.AreaName = saved.AreaName;
        profile.RememberPassword = saved.RememberPassword; profile.AutoOtp = saved.AutoOtp; profile.SteamAccount = saved.SteamAccount; profile.FreeTrial = saved.FreeTrial;
        profile.CdKeyVersion = saved.CdKeyVersion; profile.EuShopLanguage = saved.EuShopLanguage;
    }
    public static async Task DeleteAsync(LinuxSettings settings, string account, CancellationToken token,
        Func<string, CancellationToken, Task>? clear = null, bool persist = true)
    {
        var profile = settings.Current;
        if (!profile.Accounts.TryGetValue(account, out var saved)) throw new IOException("Select a saved account to delete.");
        if (clear != null) await clear(account, token);
        else await RegionCredentials.ClearAsync(settings.Root, settings.SelectedRegion, account, token);
        // Keep the record if any storage failed so deletion can be retried.
        profile.Accounts.Remove(account); profile.DeviceProfiles.Remove(account);
        if (profile.Account == account)
        {
            profile.Account = ""; profile.AreaName = "";
            profile.SteamAccount = false; profile.FreeTrial = false;
            profile.CdKeyVersion = "NA"; profile.EuShopLanguage = "en-gb";
            profile.RememberPassword = false; profile.AutoOtp = false;
        }
        if (persist) settings.Save();
    }
}
