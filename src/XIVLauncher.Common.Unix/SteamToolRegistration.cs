namespace XIVLauncher.Common.Unix;

/// <summary>Steam entry point only. This does not implement Steam account authentication.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public static class SteamToolRegistration
{
    private const string Marker = ".xivlauncher-super-owned";
    public static string ToolDirectory(string steamRoot) => Path.Combine(steamRoot, "compatibilitytools.d", "xivlauncher-super");
    public static bool IsRegistered(string steamRoot) => File.Exists(Path.Combine(ToolDirectory(steamRoot), Marker))
        && File.Exists(Path.Combine(ToolDirectory(steamRoot), "compatibilitytool.vdf"));

    public static void Install(string steamRoot, string executable)
    {
        if (!Path.IsPathRooted(steamRoot) || !Directory.Exists(Path.Combine(steamRoot, "steamapps")))
            throw new IOException("请选择包含 steamapps 的 Steam 根目录。");
        if (!Path.IsPathRooted(executable) || !File.Exists(executable)) throw new IOException("启动器可执行文件不存在。");
        // Native host binaries cannot safely run inside arbitrary Flatpak Steam sandboxes.
        if (steamRoot.Contains("/com.valvesoftware.Steam/", StringComparison.Ordinal))
            throw new IOException("暂不支持向 Flatpak Steam 注册宿主机启动器，请使用普通 Steam 安装；Proton 扫描仍可使用。");
        var dir = ToolDirectory(steamRoot);
        if (new DirectoryInfo(dir).LinkTarget != null) throw new IOException("工具目录为符号链接，未修改。");
        if (Directory.Exists(dir) && !File.Exists(Path.Combine(dir, Marker))) throw new IOException("工具目录已存在且不属于本启动器，未覆盖。");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, Marker), "XIVLauncher Super\n");
        // Steam also calls run for shader queries and install-script evaluators.
        // Those maintenance calls must not open the launcher UI.
        var script = """
#!/bin/sh
set -eu
case "${1:-}" in
  run|waitforexitandrun) ;;
  getcompatpath|getnativepath) printf '%s\n' "${2:-}"; exit 0 ;;
  *) exit 0 ;;
esac
target=$(printf '%s' "${2:-}" | tr '\\' '/' | tr '[:upper:]' '[:lower:]')
target=${target##*/}
case "$target" in
  ffxivboot.exe|ffxivboot64.exe|ffxivlauncher.exe|ffxivlauncher64.exe|ffxiv.exe|ffxiv_dx11.exe) ;;
  *) exit 0 ;;
esac
unset LD_PRELOAD LD_LIBRARY_PATH WINEPREFIX
exec
""" + " " + ShellQuote(executable) + " --steam-entry\n";
        Write(dir, "launch", script);
        File.SetUnixFileMode(Path.Combine(dir, "launch"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Write(dir, "toolmanifest.vdf", "\"manifest\" { \"version\" \"2\" \"commandline\" \"/launch %verb%\" }\n");
        Write(dir, "compatibilitytool.vdf", "\"compatibilitytools\" { \"compat_tools\" { \"xivlauncher-super\" { \"install_path\" \".\" \"display_name\" \"FFXIV Super Launcher\" \"from_oslist\" \"windows\" \"to_oslist\" \"linux\" } } }\n");
    }
    private static void Write(string dir, string name, string value)
    {
        var temp = Path.Combine(dir, name + ".tmp");
        File.WriteAllText(temp, value); File.Move(temp, Path.Combine(dir, name), true);
    }
    public static void Remove(string steamRoot)
    {
        var dir = ToolDirectory(steamRoot);
        if (!Directory.Exists(dir)) return;
        if (new DirectoryInfo(dir).LinkTarget != null || !File.Exists(Path.Combine(dir, Marker)))
            throw new IOException("工具目录不属于本启动器，未删除。");
        foreach (var file in new[] { "compatibilitytool.vdf", "toolmanifest.vdf", "launch", Marker }) File.Delete(Path.Combine(dir, file));
        if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
    }
    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
}
