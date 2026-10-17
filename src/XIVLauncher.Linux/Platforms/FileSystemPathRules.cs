namespace XIVLauncher.Linux.Platforms;

// Native filesystem identity only. URLs and archive entry names have their own rules.
internal sealed record FileSystemPathRules(char Separator, StringComparison Comparison, StringComparer Comparer)
{
    internal static FileSystemPathRules Linux { get; } = new('/', StringComparison.Ordinal, StringComparer.Ordinal);
    internal static FileSystemPathRules Windows { get; } = new('\\', StringComparison.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase);
    internal bool Same(string left, string right) => string.Equals(left, right, Comparison);
    internal bool Contains(string root, string path, bool allowRoot = false)
    {
        if (Same(root, path)) return allowRoot;
        var prefix = root.EndsWith(Separator) ? root : root + Separator;
        return path.Length > prefix.Length && path.StartsWith(prefix, Comparison);
    }
}
