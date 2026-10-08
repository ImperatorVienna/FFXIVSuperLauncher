using XIVLauncher.Common.Http;
using XIVLauncher.Linux.Patching.ZiPatch;
using XIVLauncher.Linux.Patching.ZiPatch.Util;
namespace XIVLauncher.Linux.Patching;

// Shared ZiPatch installation for ffxiv_tc and ffxiv. Source adapters own list/token protocols.
public static class ZiPatchInstaller
{
    public static async Task InstallAsync(HttpClient client, IReadOnlyList<GamePatch> patches, string game, string regionRoot,
        Action<string> progress, CancellationToken token, bool boot = false, bool writeBackup = false)
    {
        DownloadScope.Current?.BeginBatch(patches.Sum(p => p.Size));
        var cache = Path.Combine(regionRoot, "patches"); Directory.CreateDirectory(cache);
        foreach (var patch in patches)
        {
            var path = Path.Combine(cache, $"ex{patch.Repository}-{patch.Version}-{Path.GetFileName(patch.Url.AbsolutePath)}");
            progress("下载补丁：" + Path.GetFileName(path));
            await UpdateDownload.FileAsync(client, patch.Url.AbsoluteUri, path, token);
            if (!boot) await GamePatchFiles.VerifyAsync(path, patch, token);
            else if (new FileInfo(path).Length != patch.Size) throw new IOException("Boot patch size mismatch.");
            progress("安装补丁：" + patch.Version);
            await Task.Run(() =>
            {
                using var file = ZiPatchFile.FromFileName(path); using var store = new SqexFileStreamStore();
                var config = new ZiPatchConfig(Path.Combine(game, boot ? "boot" : "game")) { Store = store };
                foreach (var chunk in file.GetChunks()) { token.ThrowIfCancellationRequested(); chunk.ApplyChunk(config); }
            }, token);
            token.ThrowIfCancellationRequested();
            var versionPath = boot ? Path.Combine(game, "boot/ffxivboot.ver") : patch.Repository == 0 ? Path.Combine(game, "game/ffxivgame.ver") : Path.Combine(game, $"game/sqpack/ex{patch.Repository}/ex{patch.Repository}.ver");
            Directory.CreateDirectory(Path.GetDirectoryName(versionPath)!);
            await File.WriteAllTextAsync(versionPath + ".tmp", patch.Version, token);
            File.Move(versionPath + ".tmp", versionPath, true);
            if (writeBackup) File.Copy(versionPath, Path.ChangeExtension(versionPath, ".bck"), true);
        }
    }
}
