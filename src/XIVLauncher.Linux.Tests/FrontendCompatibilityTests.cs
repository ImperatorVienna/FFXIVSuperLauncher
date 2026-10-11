using System.Text.Json;
using XIVLauncher.Common.Unix;
using XIVLauncher.Linux.Frontend;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class FrontendCompatibilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "frontend-compatibility-" + Guid.NewGuid());
    private string Steam => Path.Combine(root, "Steam");
    private FrontendCompatibility Service(string? entry = null) => new(root, entry, () => [Steam]);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact] public void SavingDoesNotDependOnAnEnvironmentDiscoveryScan()
    {
        var service=new FrontendCompatibility(root,steamRoots:()=>throw new IOException("Synthetic scan failure"));
        var saved=service.Update(new(DataDirectory:Path.Combine(root,"committed")));
        Assert.Equal(Path.Combine(root,"committed"),saved.DataDirectory);
        Assert.Equal(saved.DataDirectory,LinuxSettings.Load(root).CompatibilityData);
        Assert.Throws<IOException>(()=>service.Read());
    }

    [Fact] public void GlobalSettingsAndGlobalPrefixHaveSeparatePersistenceBoundaries()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv_tc"; settings.Save();
        var cn = Path.Combine(root, "ffxiv_cn/settings.json"); var tc = Path.Combine(root, "ffxiv_tc/settings.json");
        var beforeCn = File.ReadAllBytes(cn); var beforeTc = File.ReadAllBytes(tc);
        var proton = Path.Combine(root, "manual/proton"); Directory.CreateDirectory(Path.GetDirectoryName(proton)!); File.WriteAllText(proton, "# synthetic");
        var service = Service(); service.Update(new(ProtonScript: proton, DataDirectory: Path.Combine(root, "shared")));
        var prefix = Path.Combine(Steam, "steamapps/compatdata/39210"); Directory.CreateDirectory(Path.Combine(prefix, "pfx/drive_c")); File.WriteAllText(Path.Combine(prefix, "pfx/system.reg"), "");
        service.Update(new(SteamPrefix: Path.Combine(prefix, "pfx"), UseSteamPrefix: true));
        var loaded = LinuxSettings.Load(root);
        Assert.Equal("ffxiv_tc", loaded.SelectedRegion); Assert.Equal(proton, loaded.ProtonScript);
        Assert.Equal(Path.Combine(root, "shared"), loaded.CompatibilityData);
        Assert.True(loaded.RegionProfiles["ffxiv"].UseSteamPrefix); Assert.Equal(prefix, loaded.RegionProfiles["ffxiv"].SteamCompatibilityData);
        Assert.Equal(beforeCn, File.ReadAllBytes(cn)); Assert.Equal(beforeTc, File.ReadAllBytes(tc));
        Assert.Single(service.Read().Runtimes); Assert.Single(service.Read().Prefixes);
        Directory.Delete(prefix, true);
        service.Update(new(UseSteamPrefix: false)); Assert.False(service.Read().UseSteamPrefix);
        Assert.ThrowsAny<IOException>(() => service.Update(new(UseSteamPrefix: true)));
        Assert.False(service.Read().UseSteamPrefix);
    }

    [Fact] public void RegistrationUsesTrustedFrontendEntryAndRemovalIsImmediate()
    {
        Directory.CreateDirectory(Path.Combine(Steam, "steamapps"));
        var entry = Path.Combine(root, "electron entry"); File.WriteAllText(entry, "#!/bin/sh\n");
        var service = Service(entry);
        var view = service.Update(new(SteamRoot: Steam, Registered: true)); Assert.True(view.Registered);
        Assert.Contains("electron entry", File.ReadAllText(Path.Combine(SteamToolRegistration.ToolDirectory(Steam), "launch")));
        Assert.Contains("--steam-entry", File.ReadAllText(Path.Combine(SteamToolRegistration.ToolDirectory(Steam), "launch")));
        service.Update(new(Registered: false)); Assert.False(SteamToolRegistration.IsRegistered(Steam));
        Assert.False(Service(entry).Read().Registered);
    }

    [Fact] public void FailedRegistrationDoesNotCommitRequestedSettingsOrDamageForeignFiles()
    {
        Directory.CreateDirectory(Path.Combine(Steam, "steamapps"));
        var entry = Path.Combine(root, "electron-entry"); File.WriteAllText(entry, "");
        var tool = SteamToolRegistration.ToolDirectory(Steam); Directory.CreateDirectory(tool);
        var foreign = Path.Combine(tool, "launch"); File.WriteAllText(foreign, "foreign launcher");
        var settings = LinuxSettings.Load(root); settings.Save();
        Assert.Throws<IOException>(() => Service(entry).Update(new(SteamRoot: Steam, Registered: true, DataDirectory: Path.Combine(root, "rejected"))));
        Assert.Equal("foreign launcher", File.ReadAllText(foreign));
        Assert.False(Service(entry).Read().Registered); Assert.Equal(settings.CompatibilityData, LinuxSettings.Load(root).CompatibilityData);
    }

    [Fact] public void MissingEntryAndInvalidPathsCannotRegisterOrCommit()
    {
        Assert.Throws<InvalidOperationException>(() => Service().Update(new(SteamRoot: Steam, Registered: true)));
        Assert.Throws<ArgumentException>(() => Service().Update(new(DataDirectory: "relative")));
        Assert.Throws<ArgumentException>(() => Service().Update(new(UseSteamPrefix: false, Registered: true)));
        Assert.False(Directory.Exists(root));
    }

    [Fact] public async Task DiagnosisCancellationLeavesHostUsableAndReleasesBusyGuard()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FrontendCompatibility(root, steamRoots: () => [], diagnose: async (_, token) =>
        { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return "unused"; });
        await using var operations = new FrontendOperations(root);
        var identity = JsonSerializer.SerializeToElement(service.Diagnose(operations)).GetProperty("operationId").GetString()!;
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => service.Diagnose(operations));
        operations.Cancel(new(identity));
        for (var i = 0; i < 100 && operations.Busy; i++) await Task.Delay(10);
        Assert.Equal("cancelled", operations.Poll(new(identity)).State);
        Assert.Equal("", service.Read().ProtonScript);
    }

    [Theory]
    [InlineData("--frontend-host", "--data-root", "/tmp/test", true)]
    [InlineData("--frontend-host", "--data-root", "relative", false)]
    [InlineData("--frontend-host", "--command", "/tmp/test", false)]
    public void HostEntryParsesOnlyExplicitDevelopmentArguments(string first, string second, string third, bool valid)
    {
        Assert.Equal(valid, FrontendStartup.TryParse([first, second, third], out _));
        Assert.True(FrontendStartup.TryParse(["--frontend-host", "--data-root", "/tmp/test", "--launcher-entry", "/tmp/test/entry", "--steam-entry"], out var result));
        Assert.True(result!.SteamEntry);
        Assert.False(FrontendStartup.TryParse(["--frontend-host", "--data-root", "/tmp/test", "--launcher-entry", "relative"], out _));
    }
}
