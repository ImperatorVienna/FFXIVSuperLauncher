using System.IO.Compression;
using System.Text.Json;
using XIVLauncher.Linux.Platforms.Windows;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class WindowsUpdateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "windows update & 测试 " + Guid.NewGuid());
    public WindowsUpdateTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static readonly string[] Required = ["FFXIVSuperLauncher.exe", "FFXIVSuperLauncher.dll", "FFXIVSuperLauncher.deps.json", "FFXIVSuperLauncher.runtimeconfig.json",
        "runtime/electron.exe", "runtime/xivlauncher-backend.exe", "runtime/frontend/main.cjs", "runtime/resources/app/main.cjs"];
    private void Populate(string directory, string value, string version)
    {
        var files = new Dictionary<string, string>();
        foreach (var name in Required)
        {
            var file = Path.Combine(directory, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, value); files.Add(name, WindowsUpdateJob.Hash(file));
        }
        File.WriteAllText(Path.Combine(directory, WindowsZipPackage.Inventory), JsonSerializer.Serialize(new WindowsPackageManifest(version, files)));
    }
    private WindowsUpdateJob Job()
    {
        Populate(root, "old", "2.1.2");
        var temporary = Path.Combine(root, "package"); var folder = Path.Combine(temporary, WindowsZipPackage.Folder);
        Populate(folder, "new", "2.2.0");
        var stage = Path.Combine(root, "update.download"); ZipFile.CreateFromDirectory(temporary, stage); Directory.Delete(temporary, true);
        return WindowsUpdateJob.Create(Path.Combine(root, "FFXIVSuperLauncher.exe"), stage, WindowsUpdateJob.Hash(stage), Path.Combine(root, "update.log"), "2.2.0", []);
    }
    [Fact] public void SuccessfulInstallPreservesUserDataAndBackup()
    {
        var job = Job(); string? started = null;
        File.WriteAllText(Path.Combine(root, "windows-settings.json"), "user-settings");
        Directory.CreateDirectory(Path.Combine(root, "ffxiv_cn")); File.WriteAllText(Path.Combine(root, "ffxiv_cn", "credential"), "private");
        job.Install("exit 0", path => started = path);
        Assert.Equal(job.Original, started); Assert.Equal("new", File.ReadAllText(job.Original));
        Assert.Equal("old", File.ReadAllText(Directory.GetFiles(Path.Combine(root, "update-backups"), "FFXIVSuperLauncher.exe", SearchOption.AllDirectories).Single()));
        Assert.Equal("user-settings", File.ReadAllText(Path.Combine(root, "windows-settings.json")));
        Assert.Equal("private", File.ReadAllText(Path.Combine(root, "ffxiv_cn", "credential")));
        Assert.False(File.Exists(job.Stage));
    }
    [Fact] public void FailedRestartRestoresEveryProgramFile()
    {
        var job = Job();
        Assert.Throws<IOException>(() => job.Install("exit 0", _ => throw new IOException("restart failed")));
        foreach (var name in Required) Assert.Equal("old", File.ReadAllText(Path.Combine(root, name)));
        Assert.True(File.Exists(job.Stage));
        Assert.Equal("FFXIVSuperLauncher.exe", File.ReadAllText(Path.Combine(root, "launcher-executable.txt")));
    }
    [Fact] public void ChangedStageOrOriginalCannotInstall()
    {
        var job = Job(); File.AppendAllText(job.Stage, "changed"); Assert.Throws<IOException>(job.Validate);
    }
    [Fact] public void ChangedOriginalCannotInstall()
    {
        var job = Job(); File.AppendAllText(job.Original, "changed"); Assert.Throws<IOException>(job.Validate);
    }
    [Fact] public void WrongVersionCannotInstall()
    { var job = Job(); Assert.Throws<IOException>(() => (job with { Version = "9.0.0" }).Validate()); }
    [Theory]
    [InlineData("../escape.dll")][InlineData("windows-settings.json")][InlineData("ffxiv_cn/credentials.json")]
    [InlineData("runtime/../../escape.exe")][InlineData("runtime/CON.txt")][InlineData("runtime/file:stream")]
    public void UserDataAndUnsafeNamesCannotEnterPackage(string name) => Assert.False(WindowsZipPackage.IsProgramFile(name));
    [Fact] public void UnlistedEntryIsRejected()
    {
        var job = Job();
        using (var zip = ZipFile.Open(job.Stage, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(zip.CreateEntry(WindowsZipPackage.Folder + "/windows-settings.json").Open())) writer.Write("overwrite");
        Assert.Throws<IOException>(() => new WindowsZipPackage().CompleteStage(job.Stage));
    }
    [Fact] public void UpdateJobRoundTripsWithGeneratedSerializer()
    {
        var job = Job(); var json = JsonSerializer.Serialize(job, WindowsUpdateJsonContext.Default.WindowsUpdateJob);
        var restored = JsonSerializer.Deserialize(json, WindowsUpdateJsonContext.Default.WindowsUpdateJob)!;
        Assert.Equal(job.Original, restored.Original); Assert.Equal(job.Version, restored.Version); restored.Validate();
    }
}
