namespace XIVLauncher.Linux.Updates;

// A stable entry outside the AppImage mount. Only this user's last launched image is selected.
internal static class AppImageEntry
{
    public const string Notice = "手动移动或重命名 AppImage 后，请双击运行一次，以更新启动路径。之后即可继续从 Steam 或应用菜单启动。多个 AppImage 共用入口时，以最后运行的文件为准。";
    public static string EntryPath => Path.Combine(LinuxSettings.DataRoot, "appimage-launcher");
    public static string RegistrationPath => Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 }
        ? Refresh(UpdateInstallation.LauncherPath, LinuxSettings.DataRoot) : UpdateInstallation.LauncherPath;
    public static string Script(string image) => "#!/bin/sh\nset -eu\nimage=" + Quote(image) + "\n" + """
if [ ! -f "$image" ]; then
  message='AppImage moved or renamed. Open the AppImage once to refresh the launcher path.'
  if command -v zenity >/dev/null 2>&1; then
    image=$(zenity --file-selection --title="$message" --file-filter='AppImage | *.AppImage') || exit 1
  elif command -v kdialog >/dev/null 2>&1; then
    image=$(kdialog --getopenfilename "$HOME" '*.AppImage' --title "$message") || exit 1
  else
    printf '%s\n' "$message" >&2
    exit 1
  fi
fi
[ -f "$image" ] && [ -x "$image" ] || exit 1
exec "$image" "$@"
""" + "\n";
    public static string Refresh(string image, string root)
    {
        LauncherUpdates.ValidateTarget(image);
        if (image.Contains('\n') || image.Contains('\r')) throw new IOException("The AppImage path cannot contain a newline.");
        LinuxSettings.EnsureCreated(root);
        var entry = Path.Combine(root, "appimage-launcher");
        var temp = entry + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, Script(image));
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.Move(temp, entry, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return entry;
    }
    private static string Quote(string text) => "'" + text.Replace("'", "'\"'\"'") + "'";
}
