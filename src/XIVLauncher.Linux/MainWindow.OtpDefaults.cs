namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private string? otpDefaultAccount;
    private long otpDefaultRevision;
    private Task otpDefaultTask = Task.CompletedTask;
    private Task RefreshOtpDefaultAsync(bool force = false)
    {
        var region = settings.SelectedRegion;
        var name = account.Text?.Trim() ?? "";
        var key = region + "\n" + name;
        if (!force && otpDefaultAccount == key) return otpDefaultTask;
        otpDefaultAccount = key;
        var revision = ++otpDefaultRevision;
        autoOneTimePassword.IsChecked = false;
        // An explicit checkbox change while lookup is pending wins over the default.
        revision = otpDefaultRevision;
        return otpDefaultTask = ApplyAsync();
        async Task ApplyAsync()
        {
            if (region is not ("ffxiv_tc" or "ffxiv") || name.Length == 0) return;
            try
            {
                // Snapshot account/provider selection; never read through mutable current-region state.
                var snapshot = LinuxSettings.Load(settings.Root);
                snapshot.SelectedRegion = region;
                var present = !string.IsNullOrWhiteSpace(await RegionCredentials.ReadAsync(snapshot, name, "otp", CancellationToken.None));
                if (revision == otpDefaultRevision && settings.SelectedRegion == region && (account.Text?.Trim() ?? "") == name)
                    autoOneTimePassword.IsChecked = present;
            }
            catch (Exception ex)
            {
                // An unreadable credential file must not make the UI unusable or silently enable OTP.
                AppendLog(DiagnosticLog.Format(ex), "DETAIL", region);
            }
        }
    }
}
