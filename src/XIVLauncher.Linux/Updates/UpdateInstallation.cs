using System.Diagnostics;
using System.Security.Cryptography;
namespace XIVLauncher.Linux.Updates;

internal static class UpdateInstallation
{
    public static string LauncherPath => Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } image
        ? Path.GetFullPath(image) : Path.Combine(AppContext.BaseDirectory, "xivlauncher-super");

    // Runs outside the AppImage mount; reopens only the launcher, never the game.
    internal const string InstallScript = """
set -eu
image=$1
stage=$2
expected=$3
original=$4
owner=$5
destination=$6
entry=$7
entryStage=$8
lock="$image.update-lock"
mkdir -- "$lock" || exit 1
trap 'rm -f -- "$entryStage"; rmdir -- "$lock"' EXIT
n=0
while [ -d "/proc/$owner" ]; do
  n=$((n+1)); [ "$n" -lt 3600 ] || exit 1
  sleep 1
done
[ ! -L "$image" ] && [ ! -L "$stage" ] && [ ! -L "$image.previous" ]
[ -f "$image" ] && [ -f "$stage" ]
[ ! -e "$destination" ] && [ ! -L "$destination" ]
[ -f "$entryStage" ] && [ ! -L "$entryStage" ]
actual=$(sha256sum -- "$stage"); actual=${actual%% *}
[ "$actual" = "$expected" ]
actual=$(sha256sum -- "$image"); actual=${actual%% *}
[ "$actual" = "$original" ]
backup="$lock/previous"
cp -p -- "$image" "$backup"
mv -f -- "$backup" "$image.previous"
chmod 700 -- "$stage"
mv -n -- "$stage" "$destination"
[ ! -e "$stage" ] || exit 1
mv -f -- "$entryStage" "$entry"
rm -- "$image"
printf '%s\n' 'Launcher update installed. Previous version: ' "$image.previous"
rmdir -- "$lock"
trap - EXIT
# Do not inherit the old, now-unmounted AppImage runtime paths.
unset APPIMAGE APPDIR ARGV0 OWD LD_LIBRARY_PATH LD_PRELOAD
cd -- "$(dirname -- "$destination")"
exec "$destination" </dev/null
""";
    public static Process Start(string image, string stage, string expected, string log, string version)
    {
        LauncherUpdates.ValidateTarget(image);
        if (Path.GetDirectoryName(stage) != Path.GetDirectoryName(image)) throw new IOException("Update must be staged beside the AppImage.");
        if (LauncherUpdates.ParseVersion(version) == null) throw new IOException("Invalid update version.");
        var destination = Path.Combine(Path.GetDirectoryName(image)!, $"xivlauncher-super-{version}-x86_64.AppImage");
        if (Path.Exists(destination)) throw new IOException("The new AppImage filename already exists; move it before updating.");
        AppImageEntry.Refresh(image, LinuxSettings.DataRoot);
        var entryStage = AppImageEntry.EntryPath + ".update-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        // Fail before closing the UI if the helper cannot write its result.
        using (var output = new FileStream(log, new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.ReadWrite,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite })) { }
        using var stream = File.OpenRead(image);
        var original = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        // Redirect inside the shell so the detached helper has no launcher-owned pipes.
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("exec >>\"$9\" 2>&1\n" + InstallScript);
        start.ArgumentList.Add("super-update");
        foreach (var value in new[] { image, stage, expected.ToLowerInvariant(), original, Environment.ProcessId.ToString(), destination, AppImageEntry.EntryPath, entryStage, log }) start.ArgumentList.Add(value);
        File.WriteAllText(entryStage, AppImageEntry.Script(destination));
        File.SetUnixFileMode(entryStage, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try { return Process.Start(start) ?? throw new IOException("Cannot start the update installer."); }
        catch { File.Delete(entryStage); throw; }
    }
}
