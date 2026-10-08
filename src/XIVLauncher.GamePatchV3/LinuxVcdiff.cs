using System.Diagnostics;
using System.Security.Cryptography;

namespace XIVLauncher.GamePatchV3;

/// <summary>Native xdelta3 worker. Replaces the Windows named-memory RPC worker on Linux.</summary>
public static class LinuxVcdiff
{
    public static async Task ApplyAsync(string executable, string sourceFile, ReadOnlyMemory<byte> delta,
        string targetFile, string expectedMd5, long expectedSize,
        IProgress<(long Progress, long Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        var temp = targetFile + ".soil-" + Guid.NewGuid().ToString("N") + ".tmp";
        var patch = temp + ".vcdiff";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetFile))!);
        try
        {
            await File.WriteAllBytesAsync(patch, delta, cancellationToken).ConfigureAwait(false);
            var psi = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true
            };
            // -D disables automatic external decompression: only decode the supplied VCDIFF bytes.
            foreach (var arg in new[] { "-d", "-D", "-f" }) psi.ArgumentList.Add(arg);
            if (File.Exists(sourceFile)) { psi.ArgumentList.Add("-s"); psi.ArgumentList.Add(sourceFile); }
            psi.ArgumentList.Add(patch); psi.ArgumentList.Add(temp);
            using var process = Process.Start(psi) ?? throw new IOException("无法启动 xdelta3 差分工具。");
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                await stdout.ConfigureAwait(false);
                var error = await stderr.ConfigureAwait(false);
                if (process.ExitCode != 0) throw new IOException($"xdelta3 差分失败 ({process.ExitCode}): {error}");
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            var size = new FileInfo(temp).Length;
            if (expectedSize >= 0 && size != expectedSize) throw new InvalidDataException("差分结果大小不符，保留原文件。");
            await using (var stream = File.OpenRead(temp))
            {
                var hash = Convert.ToHexString(await MD5.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                if (!string.IsNullOrWhiteSpace(expectedMd5) && !hash.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("差分结果 MD5 不符，保留原文件。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temp, targetFile, overwrite: true);
            progress?.Report((size, size));
        }
        finally
        {
            File.Delete(patch);
            File.Delete(temp);
        }
    }
}
