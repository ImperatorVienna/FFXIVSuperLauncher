using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms.Linux;

public sealed class AppImagePackage : IUpdatePackage
{
    public string ManifestName => "appimage-update.json";
    public string SignatureName => "appimage-update.sig";
    public void ValidateTarget(string image)
    {
        if (!Path.IsPathFullyQualified(image) || !File.Exists(image) || new FileInfo(image).LinkTarget != null)
            throw new IOException("The AppImage must be a regular file at an absolute, writable location.");
    }
    public FileStream CreateStage(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.CreateNew, Access = FileAccess.Write,
        UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
    });
    public void CompleteStage(string path)
    {
        var header = new byte[11]; using (var file = File.OpenRead(path)) file.ReadExactly(header);
        if (!header.AsSpan(0,4).SequenceEqual(new byte[] {127,69,76,70}) || header[8] != 65 || header[9] != 73 || header[10] != 2)
            throw new IOException("Update is not a type-2 AppImage.");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
