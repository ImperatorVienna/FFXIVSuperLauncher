namespace XIVLauncher.Linux.Updates;

// Shared transport verifies the signed manifest, size and checksum. The target
// platform selects its release assets and validates/prepares its package format.
public interface IUpdatePackage
{
    string ManifestName { get; }
    string SignatureName { get; }
    void ValidateTarget(string path);
    FileStream CreateStage(string path);
    void CompleteStage(string path);
}
