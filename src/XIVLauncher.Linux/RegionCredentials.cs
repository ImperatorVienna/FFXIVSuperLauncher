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
