namespace XIVLauncher.Linux;

// File paths use the host separator and comparison rules; URL/ZIP paths do not.
internal static class ContainedPath
{
    internal static string Resolve(string root, string relative, bool allowLeadingSeparator = false)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var name = relative.Replace('\\', '/');
        if (allowLeadingSeparator) name = name.TrimStart('/');
        if (name.Contains(':') || Path.IsPathRooted(name)) throw new IOException("更新文件路径超出目标目录。");
        var target = Path.GetFullPath(Path.Combine(root, name));
        Ensure(root, target, allowRoot: false, checkRootLink: false);
        return target;
    }
    internal static void Ensure(string root, string path, bool allowRoot = true, bool checkRootLink = true,
        string escapeMessage = "更新文件路径超出目标目录。", string linkMessage = "补丁目标包含符号链接。")
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var rules = Platforms.PlatformServices.Paths;
        if (!rules.Contains(root, path, allowRoot)) throw new IOException(escapeMessage);
        for (var current = path; current != null; current = Path.GetDirectoryName(current))
        {
            var atRoot = rules.Same(current, root);
            if (atRoot && !checkRootLink) break;
            if (new FileInfo(current).LinkTarget != null || new DirectoryInfo(current).LinkTarget != null)
                throw new IOException(linkMessage);
            if (atRoot) break;
        }
    }
}
