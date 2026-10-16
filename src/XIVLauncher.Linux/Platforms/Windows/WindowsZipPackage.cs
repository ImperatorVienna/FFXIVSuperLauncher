using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux.Platforms.Windows;

public sealed class WindowsZipPackage : IUpdatePackage
{
    public const string Folder = "FFXIV Super Launcher";
    internal const string Inventory = "windows-package.json";
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
    public void CompleteStage(string path) => Inspect(path);
    internal static bool IsProgramFile(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') ||
            path.Split('/').Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.') ||
                p.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 || p.Any(char.IsControl) ||
                System.Text.RegularExpressions.Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) return false;
        // All user data is beside, never inside, this distribution-owned runtime.
        if (path.StartsWith("runtime/", StringComparison.Ordinal)) return true;
        if (path.Contains('/')) return false;
        return path is "FFXIVSuperLauncher.exe" or "createdump.exe" || path == Inventory || path is "LICENSE.txt" or "ThirdPartyNotices.txt" ||
            path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
            path == "FFXIVSuperLauncher.deps.json" || path == "FFXIVSuperLauncher.runtimeconfig.json";
    }
    internal static WindowsPackageManifest ReadManifest(Stream stream)
    {
        var result = JsonSerializer.Deserialize(stream, WindowsPackageJsonContext.Default.WindowsPackageManifest)
            ?? throw new IOException("Missing Windows package inventory.");
        if (result.Files == null || result.Files.Count == 0 || result.Files.Count > 10000 || string.IsNullOrWhiteSpace(result.Version))
            throw new IOException("Invalid Windows package inventory.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, hash) in result.Files)
            if (!IsProgramFile(name) || name == Inventory || !names.Add(name) || hash == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-fA-F]{64}$")) throw new IOException("Invalid Windows package file.");
        foreach (var name in new[] { "FFXIVSuperLauncher.exe", "FFXIVSuperLauncher.dll", "FFXIVSuperLauncher.deps.json", "FFXIVSuperLauncher.runtimeconfig.json",
            "runtime/electron.exe", "runtime/xivlauncher-backend.exe", "runtime/frontend/main.cjs", "runtime/resources/app/main.cjs" })
            if (!result.Files.ContainsKey(name)) throw new IOException("Incomplete Windows package: " + name);
        return result;
    }
    internal static WindowsPackageManifest Inspect(string archive, string? extractTo = null)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 10001 || zip.Entries.Sum(e => e.Length) > 3L * 1024 * 1024 * 1024)
            throw new IOException("Windows update package is too large.");
        var manifestEntry = zip.GetEntry(Folder + "/" + Inventory) ?? throw new IOException("Windows package inventory is missing.");
        if (manifestEntry.Length > 2 * 1024 * 1024) throw new IOException("Windows package inventory is too large.");
        WindowsPackageManifest manifest;
        using (var input = manifestEntry.Open()) manifest = ReadManifest(input);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith(Folder + "/", StringComparison.Ordinal)) throw new IOException("Unexpected Windows package root.");
            var name = entry.FullName[(Folder.Length + 1)..];
            if (!IsProgramFile(name) || !seen.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new IOException("Unsafe Windows package entry.");
            if (name != Inventory)
            {
                if (!manifest.Files.TryGetValue(name, out var expected)) throw new IOException("Unlisted Windows package entry.");
                using var input = entry.Open();
                if (!Convert.ToHexString(SHA256.HashData(input)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Windows package checksum mismatch.");
            }
            if (extractTo != null)
            {
                var target = Path.Combine(extractTo, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target);
            }
        }
        if (seen.Count != manifest.Files.Count + 1) throw new IOException("Windows package files are missing.");
        return manifest;
    }
}
internal sealed record WindowsPackageManifest(string Version, Dictionary<string, string> Files);
[System.Text.Json.Serialization.JsonSerializable(typeof(WindowsPackageManifest))]
internal partial class WindowsPackageJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
