namespace XIVLauncher.Linux;

public enum RegionSyncContent { DalamudSettings, InstalledPlugins, PluginSettings }
public sealed record RegionSyncResult(string[] Targets, string BackupDirectory);

/// <summary>Replaces exactly one selected category in explicitly selected destination regions. Never copies a whole region.</summary>
public static class RegionDataSync
{
    public static string RelativePath(RegionSyncContent content) => content switch
    {
        RegionSyncContent.DalamudSettings => "dalamud/dalamudConfig.json",
        RegionSyncContent.InstalledPlugins => "dalamud/installedPlugins",
        RegionSyncContent.PluginSettings => "dalamud/pluginConfigs",
        _ => throw new ArgumentOutOfRangeException(nameof(content))
    };

    public static bool HasSource(string root, string region, RegionSyncContent content)
    {
        ValidateRegion(region);
        var path = Path.Combine(root, region, RelativePath(content));
        return content == RegionSyncContent.DalamudSettings ? File.Exists(path) : Directory.Exists(path);
    }

    public static RegionSyncResult Synchronize(string root, string sourceRegion, IReadOnlyCollection<string> targetRegions, RegionSyncContent content, CancellationToken token = default) =>
        SynchronizeCore(root, sourceRegion, targetRegions, content, token, null);

    // The hook permits testing rollback after the first destination has already been replaced.
    internal static RegionSyncResult SynchronizeCore(string root, string sourceRegion, IReadOnlyCollection<string> targetRegions, RegionSyncContent content,
        CancellationToken token, Action<int>? beforeCommit)
    {
        ValidateRegion(sourceRegion);
        ArgumentNullException.ThrowIfNull(targetRegions);
        var targets = targetRegions.ToArray();
        if (targets.Length == 0) throw new ArgumentException("Select at least one destination region.", nameof(targetRegions));
        foreach (var target in targets) ValidateRegion(target);
        if (targets.Contains(sourceRegion) || targets.Distinct(StringComparer.Ordinal).Count() != targets.Length)
            throw new ArgumentException("Destination regions must be unique and must exclude the source.", nameof(targetRegions));
        token.ThrowIfCancellationRequested();
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var relative = RelativePath(content);
        var directory = content != RegionSyncContent.DalamudSettings;
        var source = Path.Combine(root, sourceRegion, relative);
        CheckParents(root, source);
        if (!HasSource(root, sourceRegion, content)) throw new IOException("所选来源没有此项数据，未修改其他区服。");
        ValidateTree(source);
        foreach (var region in targets)
        {
            var target = Path.Combine(root, region, relative);
            CheckParents(root, target);
            if (File.Exists(target) || Directory.Exists(target))
            {
                if (directory != Directory.Exists(target)) throw new IOException("目标文件类型不匹配：" + target);
                ValidateTree(target);
            }
        }
        using var gate = new FileStream(Path.Combine(root, ".plugin-sync.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(root, ".plugin-sync-" + id);
        var backup = Path.Combine(root, "sync-backups", id);
        CheckParents(root, backup);
        PrivateDirectory(staging);
        var entries = targets.Select(region => new Entry(Path.Combine(root, region, relative),
            Path.Combine(staging, region), Path.Combine(backup, region, relative))).ToArray();
        try
        {
            // Snapshot once, then prepare all selected destinations before touching existing files.
            var snapshot = Path.Combine(staging, "source");
            Copy(source, snapshot, directory, token);
            if (!directory)
            {
                using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(snapshot));
                if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    throw new IOException("Dalamud 设置不是有效的 JSON 对象，未同步。");
            }
            foreach (var entry in entries) Copy(snapshot, entry.Prepared, directory, token);
            token.ThrowIfCancellationRequested();
            PrivateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "sync-info.txt"),
                $"Source: {sourceRegion}\nCategory: {relative}\nTargets: {string.Join(", ", targets)}\nOriginal target content is retained below each region directory.\n");
            for (var index = 0; index < entries.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                beforeCommit?.Invoke(index);
                var entry = entries[index];
                CheckParents(root, entry.Target);
                PrivateDirectory(Path.GetDirectoryName(entry.Target)!);
                if (File.Exists(entry.Target) || Directory.Exists(entry.Target))
                {
                    PrivateDirectory(Path.GetDirectoryName(entry.Backup)!);
                    Move(entry.Target, entry.Backup, directory);
                    entry.OriginalMoved = true;
                }
                Move(entry.Prepared, entry.Target, directory);
                entry.Installed = true;
            }
            return new(targets, backup);
        }
        catch (Exception original)
        {
            var failures = new List<Exception>();
            foreach (var entry in entries.Reverse())
            {
                try
                {
                    if (entry.Installed) Delete(entry.Target, directory);
                    if (entry.OriginalMoved) Move(entry.Backup, entry.Target, directory);
                }
                catch (Exception recovery) { failures.Add(recovery); }
            }
            if (failures.Count != 0)
                throw new AggregateException("同步失败且部分目标未能还原；原数据备份位于：" + backup, new[] { original }.Concat(failures));
            throw;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    private static void ValidateRegion(string region)
    {
        if (!LinuxSettings.Regions.Any(r => r.Id == region)) throw new ArgumentException("未知来源区服。", nameof(region));
    }
    private static void CheckParents(string root, string path)
    {
        for (var current = path; ; current = Path.GetDirectoryName(current)!)
        {
            RejectLink(current);
            if (current == root) break;
            if (string.IsNullOrEmpty(current)) throw new IOException("同步路径超出数据目录。");
        }
    }
    private static void RejectLink(string path)
    {
        if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new IOException("同步路径含符号链接，未自动跟随：" + path);
    }
    private static void ValidateTree(string path)
    {
        RejectLink(path);
        if (Directory.Exists(path))
            foreach (var child in Directory.EnumerateFileSystemEntries(path)) ValidateTree(child);
    }
    private static void Copy(string source, string target, bool directory, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RejectLink(source);
        if (directory)
        {
            PrivateDirectory(target);
            foreach (var child in Directory.EnumerateFileSystemEntries(source))
                Copy(child, Path.Combine(target, Path.GetFileName(child)), Directory.Exists(child), token);
        }
        else
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(target, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
            if ((File.GetUnixFileMode(source) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var buffer = new byte[128 * 1024];
            int read;
            while ((read = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
        }
    }
    private static void PrivateDirectory(string path) => Directory.CreateDirectory(path,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    private static void Move(string from, string to, bool directory)
    {
        if (directory) Directory.Move(from, to); else File.Move(from, to);
    }
    private static void Delete(string path, bool directory)
    {
        if (directory) Directory.Delete(path, true); else File.Delete(path);
    }
    private sealed class Entry(string target, string prepared, string backup)
    {
        public string Target { get; } = target;
        public string Prepared { get; } = prepared;
        public string Backup { get; } = backup;
        public bool OriginalMoved { get; set; }
        public bool Installed { get; set; }
    }
}
