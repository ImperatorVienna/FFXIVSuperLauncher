using XIVLauncher.Linux.Patching;
using System.Diagnostics;
using System.Security.Cryptography;
using XIVLauncher.GamePatchV3;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class GamePatchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "soil-patch-test-" + Guid.NewGuid());
    private static string Worker => Path.Combine(AppContext.BaseDirectory, "Tools/xdelta3");

    private async Task<(string Source, string Target, byte[] Delta, byte[] Expected)> MakeDelta()
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source with 空格.dat");
        var target = Path.Combine(root, "target.dat");
        var patch = Path.Combine(root, "delta.dat");
        var original = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(original);
        await File.WriteAllBytesAsync(source, original);
        var expected = original.ToArray();
        expected[3333] ^= 0x7F;
        await File.WriteAllBytesAsync(target, expected);
        var psi = new ProcessStartInfo(Worker) { UseShellExecute = false };
        foreach (var arg in new[] { "-e", "-s", source, target, patch }) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        return (source, target, await File.ReadAllBytesAsync(patch), expected);
    }

    [Fact]
    public async Task RealVcdiffIsVerifiedThenAtomicallyReplacesSourceInPlace()
    {
        var data = await MakeDelta();
        using var client = new VcdiffClient(Worker);
        await client.ApplyVcdiff(data.Source, data.Delta, data.Source,
            Convert.ToHexString(MD5.HashData(data.Expected)), data.Expected.Length);
        Assert.Equal(data.Expected, await File.ReadAllBytesAsync(data.Source));
        Assert.Empty(Directory.GetFiles(root, "*.tmp*"));
    }

    [Fact]
    public async Task InvalidHashLeavesOriginalFileIntact()
    {
        var data = await MakeDelta();
        var original = await File.ReadAllBytesAsync(data.Source);
        await Assert.ThrowsAsync<InvalidDataException>(() => LinuxVcdiff.ApplyAsync(Worker, data.Source,
            data.Delta, data.Source, new string('0', 32), data.Expected.Length));
        Assert.Equal(original, await File.ReadAllBytesAsync(data.Source));
        Assert.Empty(Directory.GetFiles(root, "*.tmp*"));
    }

    [Fact]
    public async Task CancellationDoesNotReplaceOriginalFile()
    {
        var data = await MakeDelta();
        var original = await File.ReadAllBytesAsync(data.Source);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LinuxVcdiff.ApplyAsync(Worker,
            data.Source, data.Delta, data.Source, "", data.Expected.Length, cancellationToken: cts.Token));
        Assert.Equal(original, await File.ReadAllBytesAsync(data.Source));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
