using System.Buffers.Binary;
using System.Text.Json;
using XIVLauncher.Linux.Platforms.Windows;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class WindowsUpdateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "windows update & 测试 " + Guid.NewGuid());
    public WindowsUpdateTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private WindowsUpdateJob Job()
    {
        var original = Path.Combine(root, "old launcher.exe"); File.WriteAllText(original, "original");
        var stage = Path.Combine(root, "new.download");
        var bytes = new byte[512];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c), 0x80);
        bytes[0x80] = (byte)'P'; bytes[0x81] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), 240);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98), 0x20b);
        File.WriteAllBytes(stage, bytes);
        return WindowsUpdateJob.Create(original, stage, WindowsUpdateJob.Hash(stage), Path.Combine(root,"update.log"), "2.2.0", []);
    }
    [Fact] public void SuccessfulInstallPreservesBackupAndRefreshesPortableEntry()
    {
        var job = Job(); string? started = null;
        job.Install("exit 0", path => started = path);
        Assert.Equal(job.Destination, started); Assert.False(File.Exists(job.Original));
        Assert.Equal("original", File.ReadAllText(job.Original + ".previous"));
        Assert.Equal(Path.GetFileName(job.Destination), File.ReadAllText(Path.Combine(root,"launcher-executable.txt")));
        Assert.Equal(job.ExpectedHash, WindowsUpdateJob.Hash(job.Destination));
    }
    [Fact] public void RestartFailureRestoresOldEntryAndRetainsDownloadedStage()
    {
        var job = Job();
        Assert.Throws<IOException>(() => job.Install("exit 0", _ => throw new IOException("Synthetic restart failure")));
        Assert.Equal("original", File.ReadAllText(job.Original)); Assert.True(File.Exists(job.Stage));
        Assert.False(File.Exists(job.Destination));
        Assert.Equal(Path.GetFileName(job.Original), File.ReadAllText(Path.Combine(root,"launcher-executable.txt")));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void ChangedFilesAreRejectedBeforeAnyReplacement(bool stage)
    {
        var job = Job(); File.AppendAllText(stage ? job.Stage : job.Original, "changed");
        Assert.Throws<IOException>(() => job.Install("exit 0", _ => Assert.Fail("Must not launch")));
        Assert.True(File.Exists(job.Original)); Assert.False(File.Exists(job.Destination));
    }
    [Fact] public void RejectsExistingDestinationAndEscapingStage()
    {
        var job = Job(); File.WriteAllText(job.Destination, "user file");
        Assert.Throws<IOException>(() => job.Validate());
        Assert.Equal("user file", File.ReadAllText(job.Destination));
        File.Delete(job.Destination);
        var dir = Path.Combine(root,"elsewhere"); Directory.CreateDirectory(dir);
        var other = Path.Combine(dir,"stage"); File.Copy(job.Stage,other);
        Assert.Throws<IOException>(() => (job with { Stage = other }).Validate());
    }
    [Fact] public void JobUsesAotCompatibleDataSerialization()
    {
        var job = Job(); var json = JsonSerializer.Serialize(job, WindowsUpdateJsonContext.Default.WindowsUpdateJob);
        var restored = JsonSerializer.Deserialize(json, WindowsUpdateJsonContext.Default.WindowsUpdateJob)!;
        Assert.Equal(job.Original,restored.Original); Assert.Equal(job.ExpectedHash,restored.ExpectedHash); restored.Validate();
    }
}
