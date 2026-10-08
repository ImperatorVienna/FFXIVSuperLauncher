using System.Diagnostics;
using System.Text.Json;
using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Common.Unix;
using XIVLauncher.Linux;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class SuperSettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-tests-" + Guid.NewGuid().ToString("N"));
    public SuperSettingsTests() => Directory.CreateDirectory(root);
    [Fact]
    public void RegionSettingsPreserveIdentityAndKeepGlobalToolsSeparate()
    {
        var settings = LinuxSettings.Load(root);
        settings.GamePath = "/cn/game"; settings.ProtonScript = "/shared/proton";
        settings.CompatibilityData = "/external/prefix";
        settings.RegionProfiles["ffxiv_tc"].GamePath = "/tc/game";
        var identity = settings.GetDeviceProfile("test"); settings.Save();
        var loaded = LinuxSettings.Load(root);
        Assert.Equal(identity, loaded.GetDeviceProfile("test"));
        Assert.Equal("/cn/game", loaded.GamePath);
        Assert.Equal("/tc/game", loaded.RegionProfiles["ffxiv_tc"].GamePath);
        Assert.Equal("/external/prefix", loaded.CompatibilityData);
        using var global = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "linux-settings.json")));
        Assert.False(global.RootElement.TryGetProperty("DeviceProfiles", out _));
    }
    [Fact]
    public void SupportedRegionsPreserveSharedCompatibilityTools()
    {
        var settings = LinuxSettings.Load(root); settings.ProtonScript = "/shared/proton";
        settings.SelectedRegion = "ffxiv_tc";
        settings.EnsureRegionAvailable();
        Assert.NotNull(RegionBackends.For("ffxiv_tc"));
        settings.SelectedRegion = "ffxiv";
        settings.EnsureRegionAvailable();
        Assert.NotNull(RegionBackends.For("ffxiv"));
        settings.Save();
        Assert.Equal("/shared/proton", LinuxSettings.Load(root).ProtonScript);
        Assert.False(settings.SetupComplete);
        Assert.Empty(settings.RegisteredSteamRoot);
    }
    [Fact]
    public async Task SteamToolQuotesPathsAndDoesNotForwardSteamLoginArguments()
    {
        Directory.CreateDirectory(Path.Combine(root, "steamapps"));
        var executable = Path.Combine(root, "launcher ' special");
        var output = Path.Combine(root, "arguments");
        File.WriteAllText(executable, "#!/bin/sh\nprintf '%s\\n' \"$@\" > " + "'" + output + "'\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        SteamToolRegistration.Install(root, executable);
        Assert.True(SteamToolRegistration.IsRegistered(root));
        var info = new ProcessStartInfo(Path.Combine(SteamToolRegistration.ToolDirectory(root), "launch")) { UseShellExecute = false };
        info.ArgumentList.Add("waitforexitandrun"); info.ArgumentList.Add("ffxivboot.exe"); info.ArgumentList.Add("--steam-login");
        using var process = Process.Start(info)!; await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode); Assert.Equal("--steam-entry\n", File.ReadAllText(output));
        SteamToolRegistration.Install(root, executable); // repeated enable updates registration
        File.WriteAllText(Path.Combine(SteamToolRegistration.ToolDirectory(root), "user-file"), "keep");
        SteamToolRegistration.Remove(root);
        Assert.False(SteamToolRegistration.IsRegistered(root));
        Assert.True(File.Exists(Path.Combine(SteamToolRegistration.ToolDirectory(root), "user-file")));
    }
    [Theory]
    [InlineData("run", "d3ddriverquery64.exe")]
    [InlineData("run", "/steam/iscriptevaluator.exe")]
    [InlineData("waitforexitandrun", "d3ddriverquery64.exe")]
    [InlineData("run", "")]
    [InlineData("run", "/other/game.exe")]
    public async Task SteamMaintenanceCallsDoNotOpenLauncher(string verb, string target)
    {
        Directory.CreateDirectory(Path.Combine(root,"steamapps"));
        var executable=Path.Combine(root,"fake-launcher"); var output=Path.Combine(root,"opened");
        File.WriteAllText(executable,"#!/bin/sh\ntouch '"+output+"'\n");
        File.SetUnixFileMode(executable,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        SteamToolRegistration.Install(root,executable);
        var psi=new ProcessStartInfo(Path.Combine(SteamToolRegistration.ToolDirectory(root),"launch")){UseShellExecute=false};
        psi.ArgumentList.Add(verb);psi.ArgumentList.Add(target);
        using var process=Process.Start(psi)!;await process.WaitForExitAsync();
        Assert.Equal(0,process.ExitCode);Assert.False(File.Exists(output));
    }
    [Theory]
    [InlineData("/steam library/FFXIV/boot/ffxivboot.exe")]
    [InlineData(@"C:\Steam Library\FFXIV\boot\FFXIVBOOT64.EXE")]
    [InlineData("ffxivlauncher64.exe")]
    public async Task SteamFfxivPathsStillOpenLauncher(string target)
    {
        Directory.CreateDirectory(Path.Combine(root,"steamapps"));
        var executable=Path.Combine(root,"fake-launcher");var output=Path.Combine(root,"opened");
        File.WriteAllText(executable,"#!/bin/sh\nprintf '%s' \"$1\" > '"+output+"'\n");
        File.SetUnixFileMode(executable,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        SteamToolRegistration.Install(root,executable);
        var psi=new ProcessStartInfo(Path.Combine(SteamToolRegistration.ToolDirectory(root),"launch")){UseShellExecute=false};
        psi.ArgumentList.Add("waitforexitandrun");psi.ArgumentList.Add(target);
        using var process=Process.Start(psi)!;await process.WaitForExitAsync();
        Assert.Equal(0,process.ExitCode);Assert.Equal("--steam-entry",File.ReadAllText(output));
    }

    [Fact]
    public void SteamToolDoesNotOverwriteUnownedDirectory()
    {
        Directory.CreateDirectory(Path.Combine(root, "steamapps"));
        Directory.CreateDirectory(SteamToolRegistration.ToolDirectory(root));
        Assert.Throws<IOException>(() => SteamToolRegistration.Install(root, "/bin/true"));
        Assert.Throws<IOException>(() => SteamToolRegistration.Remove(root));
    }
    [Fact]
    public async Task ZeroExitWithoutConfirmationIsUnconfirmedRatherThanFailed()
    {
        var psi = new ProcessStartInfo("/bin/true") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new()));
        await Assert.ThrowsAsync<LaunchUnconfirmedException>(() => runner.LaunchAsync(psi, ""));
    }
    [Fact]
    public async Task InjectorCanConfirmOnStderr()
    {
        var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("echo '{\"pid\":321}' >&2");
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new()));
        Assert.False(psi.Environment.ContainsKey("DALAMUD_FORCE_MINHOOK"));
        Assert.Equal(321, await runner.LaunchAsync(psi, ""));
    }
    [Fact]
    public async Task MultilineInjectorResponseIsAccepted()
    {
        var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("printf '{\n  \"pid\": 42\n}\n'");
        Assert.Equal(42, await new UnixDalamudRunner(new CompatibilityRunner(new())).LaunchAsync(psi, ""));
    }
    [Fact]
    public async Task TimeoutDoesNotKillPossiblyRunningGame()
    {
        var marker = Path.Combine(root, "still-running");
        var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("sleep 0.2; touch '" + marker + "'");
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new()));
        await Assert.ThrowsAsync<LaunchUnconfirmedException>(() => runner.LaunchAsync(psi, "", confirmationTimeout: TimeSpan.FromMilliseconds(30)));
        await Task.Delay(400);
        Assert.True(File.Exists(marker));
    }
    [Fact]
    public async Task PreparedInjectorPreservesResponseInsteadOfUsingSteamTrampoline()
    {
        var proton = Path.Combine(root, "proton");
        File.WriteAllText(proton, "#!/usr/bin/python3\nimport sys\nif sys.argv[2] == 'winepath': print('Z:\\\\tmp')\nelif sys.argv[1] == 'runinprefix': print('{\"pid\":321}')\n");
        File.SetUnixFileMode(proton, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var exe = Path.Combine(root, "injector.exe"); File.WriteAllText(exe, "");
        var compatibility = new CompatibilityRunner(new() { Executable = proton, SteamRoot = root, DataDirectory = Path.Combine(root, "compatdata") });
        var runner = new UnixDalamudRunner(compatibility);
        var psi = await runner.PrepareAsync(new(exe), new(exe), new(root), new()
        {
            WorkingDirectory = root, ConfigurationPath = Path.Combine(root, "config.json"), LoggingPath = root,
            PluginDirectory = root, AssetDirectory = root, LauncherDirectory = root
        }, true, false);
        Assert.False(psi.Environment.ContainsKey("DALAMUD_FORCE_MINHOOK"));
        Assert.Equal(321, await runner.LaunchAsync(psi, ""));
    }

    [Fact]
    public async Task TaiwanUsesSharedEntrypointWithoutAttachHelper()
    {
        var proton = Path.Combine(root, "proton");
        File.WriteAllText(proton, "#!/usr/bin/python3\nimport sys\nprint('Z:/fixture')\n");
        File.SetUnixFileMode(proton, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var exe = Path.Combine(root, "injector.exe"); File.WriteAllText(exe, "");
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new() { Executable = proton, SteamRoot = root, DataDirectory = Path.Combine(root, "compatdata") }));
        var psi = await runner.PrepareAsync(new(exe), new(exe), new(root), new()
        {
            WorkingDirectory = root, ConfigurationPath = Path.Combine(root, "config.json"), LoggingPath = root,
            PluginDirectory = root, AssetDirectory = root, LauncherDirectory = root
        }, true, true, soil: false);
        Assert.False(psi.Environment.ContainsKey("DALAMUD_FORCE_MINHOOK"));
        Assert.Contains("launch", psi.ArgumentList);
        Assert.Contains("--mode=entrypoint", psi.ArgumentList);
        Assert.Contains(psi.ArgumentList, x => x.StartsWith("--game="));
        Assert.Contains("--no-plugin", psi.ArgumentList);
        Assert.Contains("--dalamud-platform=linux", psi.ArgumentList);
        Assert.Equal("--", psi.ArgumentList.Last());
    }

    [Fact]
    public void MountFilteringPreservesExternalLibrariesAndAvoidsReservedTrees()
    {
        Assert.Equal("/home/user/game:/opt/launcher:/usr-other", CompatibilityRunner.FilterMounts(
            "/usr:/usr/share/steam/proton:/lib64:/etc/config:/home/user/game:/opt/launcher:/home/user/game:/usr-other"));
    }

    [Fact]
    public void FreshConfigurationUsesProvidedRootWithoutImportingSiblingData()
    {
        var old = Path.Combine(root, "xivlauncher-cn-soil"); Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "linux-settings.json"), "old settings");
        var fresh = Path.Combine(root, "xivlauncher-super");
        var settings = LinuxSettings.Load(fresh); settings.Save();
        Assert.Equal(Path.Combine(fresh, "compatdata"), settings.CompatibilityData);
        Assert.Empty(settings.GamePath); Assert.False(settings.SetupComplete);
        Assert.Equal("old settings", File.ReadAllText(Path.Combine(old, "linux-settings.json")));
    }
    public void Dispose() => Directory.Delete(root, true);
}
