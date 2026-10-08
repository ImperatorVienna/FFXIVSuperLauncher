using System.Text.Json;
using XIVLauncher.Linux.Taiwan;
namespace XIVLauncher.Linux;

public static class RegionCredentials
{
    public static Task<string?> ReadAsync(LinuxSettings settings, string account, string kind, CancellationToken token) =>
        ReadAsync(settings.Root, settings.SelectedRegion, account, kind, token);
    public static Task<string?> ReadAsync(string root, string region, string account, string kind, CancellationToken token)
    {
        Validate(region, account, kind);
        token.ThrowIfCancellationRequested(); var path = FilePath(root, region);
        var data = Load(path); return Task.FromResult(data.TryGetValue(account, out var entry) ? entry.GetValueOrDefault(kind) : null);
    }
    public static async Task StoreAsync(LinuxSettings settings, string account, string kind, string secret, CancellationToken token)
    {
        await StoreValueAsync(settings.Root, settings.SelectedRegion, account, kind, secret, token);
        AccountProfiles.Capture(settings.Current);
        settings.Save();
    }
    internal static Task StoreValueAsync(string root, string region, string account, string kind, string secret, CancellationToken token)
    {
        Validate(region, account, kind);
        token.ThrowIfCancellationRequested(); var path = FilePath(root, region); var data = Load(path);
        if (!data.TryGetValue(account, out var entry)) data[account] = entry = new();
        entry[kind] = secret; Save(path, data); return Task.CompletedTask;
    }
    public static Task ClearAsync(LinuxSettings settings, string account, CancellationToken token) =>
        ClearAsync(settings.Root, settings.SelectedRegion, account, token);
    public static Task ClearAsync(string root, string region, string account, CancellationToken token)
    {
        Validate(region, account, "password");
        token.ThrowIfCancellationRequested(); var path = FilePath(root, region);
        var data = Load(path); if (data.Remove(account)) Save(path, data); return Task.CompletedTask;
    }
    public static string[] SavedAccountNames(string root, string region)
    {
        try { return Load(FilePath(root, region)).Keys.ToArray(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }
    public static Task RemoveAsync(LinuxSettings settings, string account, string kind, CancellationToken token) =>
        RemoveValueAsync(settings.Root, settings.SelectedRegion, account, kind, token);
    internal static Task RemoveValueAsync(string root, string region, string account, string kind, CancellationToken token)
    {
        Validate(region, account, kind);
        token.ThrowIfCancellationRequested(); var path = FilePath(root, region); var data = Load(path);
        if (data.TryGetValue(account, out var entry) && entry.Remove(kind)) { if (entry.Count == 0) data.Remove(account); Save(path, data); }
        return Task.CompletedTask;
    }
    public static async Task<int> SaveEnteredAsync(LinuxSettings settings, string password, string otpSecret, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(settings.Account)) throw new CredentialValidationException("An account is required to save credentials.", "请先填写或选择账号，再保存凭据。");
        if (settings.SelectedRegion is "ffxiv_tc" or "ffxiv")
        {
            if (string.IsNullOrEmpty(password))
                throw new CredentialValidationException("Enter a password to save password credentials; a 2FA secret cannot be saved alone.", "请填写密码后再保存；填写了 2FA 密钥时，必须同时填写密码，不能单独保存密钥。");
            if (!settings.Current.RememberPassword) throw new CredentialValidationException("Enable remember password before saving password credentials.", "请先勾选“记住登录密码”，再保存凭据。");
        }
        // Validate before any write so an invalid OTP secret cannot cause a misleading partial save.
        if ((settings.SelectedRegion is "ffxiv_tc" or "ffxiv") && !string.IsNullOrWhiteSpace(otpSecret))
        {
            try { _ = OneTimePassword.Decode(otpSecret); }
            catch (ArgumentException) { throw new CredentialValidationException("Invalid 2FA secret; credentials were not saved.", "2FA 密钥格式无效，未保存凭据。请填写 Base32 密钥，不是六位验证码。"); }
        }
        var count = 0;
        if (settings.Current.RememberPassword && !string.IsNullOrEmpty(password))
        { await StoreAsync(settings, settings.Account, "password", password, token); count++; }
        if ((settings.SelectedRegion is "ffxiv_tc" or "ffxiv") && !string.IsNullOrWhiteSpace(otpSecret))
        { await StoreAsync(settings, settings.Account, "otp", otpSecret, token); count++; }
        return count;
    }
    public static async Task<string> PasswordForLoginAsync(LinuxSettings settings, string entered, CancellationToken token)
    {
        if (!string.IsNullOrEmpty(entered)) return entered;
        return settings.Current.RememberPassword ? await ReadAsync(settings, settings.Account, "password", token) ?? "" : "";
    }
    private static void Validate(string region, string account, string kind)
    {
        if (!LinuxSettings.Regions.Any(r => r.Id == region) || string.IsNullOrWhiteSpace(account) || kind is not ("password" or "otp" or "session"))
            throw new ArgumentException("凭据区服、账号或类型无效。");
    }
    private static string FilePath(string root, string region)
    {
        var path = Path.Combine(Path.GetFullPath(root), region, "credentials.json");
        for (var p = path; p != null; p = Path.GetDirectoryName(p))
            if (new FileInfo(p).LinkTarget != null || new DirectoryInfo(p).LinkTarget != null) throw new IOException("凭据路径包含符号链接，未写入。");
        return path;
    }
    private static Dictionary<string, Dictionary<string, string>> Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)) ?? throw new IOException("凭据文件格式错误。") : new();
    private static void Save(string path, Dictionary<string, Dictionary<string, string>> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }))
                JsonSerializer.Serialize(stream, data, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
