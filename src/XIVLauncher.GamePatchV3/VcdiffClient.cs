namespace XIVLauncher.GamePatchV3;

/// <summary>Runs native xdelta3 with bounded parallelism.</summary>
public sealed class VcdiffClient(string workerExecutablePath) : IDisposable
{
    private readonly SemaphoreSlim channelGate = new(Math.Clamp(Environment.ProcessorCount, 1, 4));

    public void Dispose() => channelGate.Dispose();

    public async Task ApplyVcdiff(
        string sourceFile, ReadOnlyMemory<byte> deltaData, string targetFile,
        string expectedMd5, long expectedSize,
        IProgress<(long Progress, long Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await channelGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LinuxVcdiff.ApplyAsync(workerExecutablePath, sourceFile, deltaData, targetFile,
                expectedMd5, expectedSize, progress, cancellationToken).ConfigureAwait(false);
        }
        finally { channelGate.Release(); }
    }
}
