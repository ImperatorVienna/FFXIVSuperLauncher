namespace XIVLauncher.Linux;
public static class LoginCode
{
    public static async Task<string?> SavedSecretForLoginAsync(LinuxSettings settings, CancellationToken token)
    {
        if (settings.SelectedRegion is not ("ffxiv_tc" or "ffxiv") || !settings.Current.AutoOtp || string.IsNullOrWhiteSpace(settings.Account)) return null;
        var secret = await RegionCredentials.ReadAsync(settings, settings.Account, "otp", token);
        if (string.IsNullOrWhiteSpace(secret)) return null;
        try { _ = OneTimePassword.Decode(secret); return secret; }
        catch (ArgumentException) { return null; }
    }
    public static async Task<string> GetAsync(string? secret, Func<CancellationToken, Task<string>> manual, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var value = secret != null ? OneTimePassword.Generate(secret, DateTimeOffset.UtcNow) : await manual(token);
        token.ThrowIfCancellationRequested();
        if (value.Length != 0 && !OneTimePassword.IsManualCode(value)) throw new IOException("2FA code must contain six digits.");
        return value;
    }
}
