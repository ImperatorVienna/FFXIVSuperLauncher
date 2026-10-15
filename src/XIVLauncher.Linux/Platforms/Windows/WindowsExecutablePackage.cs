using System.Reflection.PortableExecutable;
using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms.Windows;

public sealed class WindowsExecutablePackage : IUpdatePackage
{
    public string ManifestName => "windows-x64-update.json";
    public string SignatureName => "windows-x64-update.sig";
    public void ValidateTarget(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
            !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The Windows launcher must be a regular EXE at an absolute location.");
    }
    public FileStream CreateStage(string path) => new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    public void CompleteStage(string path)
    {
        using var file = File.OpenRead(path);
        using var pe = new PEReader(file);
        if (pe.PEHeaders.PEHeader == null || pe.PEHeaders.CoffHeader.Machine != Machine.Amd64 ||
            (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.ExecutableImage) == 0 ||
            (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0)
            throw new IOException("Update is not a Windows x64 executable.");
    }
}
