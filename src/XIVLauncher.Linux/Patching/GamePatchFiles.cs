using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace XIVLauncher.Linux.Patching;

public sealed record GamePatch(long Size, string Version, int Repository, int BlockSize, string[] Hashes, Uri Url);

public static class GamePatchFiles
{
    internal static readonly Regex VersionPattern = new(@"^\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}$");
    public static string Version(string game, int repository = 0)
    {
        var file = repository == 0 ? Path.Combine(game, "game/ffxivgame.ver") : Path.Combine(game, $"game/sqpack/ex{repository}/ex{repository}.ver");
        var value = File.ReadAllText(file).Trim();
        if (!VersionPattern.IsMatch(value)) throw new IOException("Client version file is invalid.");
        return value;
    }
    internal static async Task VerifyAsync(string path, GamePatch patch, CancellationToken token)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length != patch.Size) throw new IOException("Patch size mismatch.");
        var buffer = new byte[128 * 1024];
        foreach (var expected in patch.Hashes)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            long remaining = Math.Min(patch.BlockSize, stream.Length - stream.Position);
            while (remaining > 0)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
                if (read == 0) throw new EndOfStreamException();
                hash.AppendData(buffer, 0, read); remaining -= read;
            }
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Patch SHA1 verification failed.");
        }
    }
}
