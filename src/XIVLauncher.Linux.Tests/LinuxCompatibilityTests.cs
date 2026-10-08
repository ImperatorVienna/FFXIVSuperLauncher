using System.Diagnostics;
using System.Text.Json;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Unix;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class LinuxCompatibilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "soil-tests-" + Guid.NewGuid());
    private string Write(string relative, string content = "")
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void DiscoveryFindsSystemCustomAndExternalSteamToolsAndTheirRuntime()
    {
        var steam = Path.Combine(root, "Steam");
        var library = Path.Combine(root, "external games");
        Write("Steam/steamapps/libraryfolders.vdf", $"\"libraryfolders\" {{ \"1\" {{ \"path\" \"{library}\" }} }}");
        Write("Steam/compatibilitytools.d/GE-Proton/proton");
        Write("external games/steamapps/common/Proton Experimental/proton");
        Write("system/proton-cachyos/proton");
        Write("system/proton-cachyos/compatibilitytool.vdf", "\"compatibilitytools\" { \"display_name\" \"CachyOS Proton\" }");
        Write("system/proton-cachyos/toolmanifest.vdf", "\"manifest\" { \"require_tool_appid\" \"4183110\" }");
        Write("external games/steamapps/appmanifest_4183110.acf", "\"AppState\" { \"installdir\" \"SteamLinuxRuntime_4\" }");
        var entry = Write("external games/steamapps/common/SteamLinuxRuntime_4/_v2-entry-point");
        var alias = Path.Combine(root, "steam-alias");
        Directory.CreateSymbolicLink(alias, steam);
        var tools = ProtonDiscovery.Discover([steam, alias], [Path.Combine(root, "system")]);
        Assert.Equal(3, tools.Count);
        var cachyos = Assert.Single(tools, x => x.Name == "CachyOS Proton");
        Assert.True(cachyos.IsReady);
        Assert.Equal(entry, cachyos.RuntimeEntryPoint);
        Assert.Equal(steam, cachyos.SteamRoot);
    }

    [Fact]
    public void DiscoveryReportsMissingRuntimeInsteadOfSilentlyUsingHostLibraries()
    {
        Write("tools/custom/proton");
        Write("tools/custom/toolmanifest.vdf", "\"require_tool_appid\" \"1628350\"");
        var tool = Assert.Single(ProtonDiscovery.Discover([], [Path.Combine(root, "tools")]));
        Assert.False(tool.IsReady);
        Assert.Equal("1628350", tool.RequiredRuntimeAppId);
    }

    [Fact]
    public void ProtonUsesItsScriptAndCompatdataParentInsideRuntime()
    {
        var runner = new CompatibilityRunner(new()
        {
            Executable = "/tools/Proton GE/proton", DataDirectory = "/games/soil data", SteamRoot = "/steam",
            RuntimeEntryPoint = "/runtime/_v2-entry-point"
        });
        var psi = runner.BuildStartInfo(["/game path/ffxiv.exe", "a=b c"], "/game path", mainSession: true);
        Assert.Equal("/runtime/_v2-entry-point", psi.FileName);
        Assert.Equal(new[] { "--verb=waitforexitandrun", "--", "/tools/Proton GE/proton", "run", "/game path/ffxiv.exe", "a=b c" }, psi.ArgumentList);
        Assert.Equal("/games/soil data", psi.Environment["STEAM_COMPAT_DATA_PATH"]);
        Assert.False(psi.Environment.ContainsKey("WINEPREFIX"));
        Assert.False(psi.UseShellExecute);
    }

    [Theory]
    [InlineData("", false, 0)]
    [InlineData("some log with pid: 17", false, 0)]
    [InlineData("{\"pid\":0}", false, 0)]
    [InlineData("{\"pid\":\"123\"}", false, 0)]
    [InlineData("{\"pid\":88,\"handle\":1234}", true, 88)]
    public void OnlyStructuredPositiveWinePidsAreAccepted(string line, bool expected, int pid)
    {
        Assert.Equal(expected, UnixDalamudRunner.TryReadWinePid(line, out var actual));
        if (expected) Assert.Equal(pid, actual);
    }

    [Fact]
    public async Task ArgumentQuotingSurvivesRealProcessLaunchWithoutShellExpansion()
    {
        var script = Write("args.py", "import sys,json; print(json.dumps(sys.argv[1:]))");
        string[] values = ["", "/a path/中文", "a\"b", "C:\\a b\\", "$(touch NEVER_RUN)", "a\\\"b"];
        using var process = Process.Start(new ProcessStartInfo("/usr/bin/python3")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            Arguments = string.Join(' ', new[] { script }.Concat(values).Select(UnixDalamudRunner.QuoteArgument))
        })!;
        var parsed = JsonSerializer.Deserialize<string[]>(await process.StandardOutput.ReadToEndAsync());
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(values, parsed);
    }

    [Fact]
    public async Task CaptureDrainsBothStreamsAndPropagatesFailure()
    {
        var script = Write("fake-proton", "#!/usr/bin/python3\nimport sys\nsys.stderr.write('e'*200000)\nprint('failed')\nsys.exit(23)\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runner = new CompatibilityRunner(new() { Executable = script, DataDirectory = root });
        var error = await Assert.ThrowsAsync<IOException>(() => runner.CaptureAsync(["cmd"]));
        Assert.Contains("23", error.Message);
    }

    [Fact]
    public void GameRequestPreservesSoilCnArguments()
    {
        Write("ffxiv/game/ffxiv_dx11.exe");
        var request = new Launcher().CreateGameStartRequest("ticket", "snda", 0, "area", "lobby", "gm", "db",
            "areas", "", new DirectoryInfo(Path.Combine(root, "ffxiv")));
        Assert.Contains("DEV.TestSID=ticket", request.Arguments);
        Assert.Contains("XL.SndaId=snda", request.Arguments);
        Assert.Contains("XL.LobbyHosts=areas", request.Arguments);
        Assert.Equal(Path.Combine(root, "ffxiv/game"), request.WorkingDirectory);
    }

    [Fact]
    public async Task InjectorWaitsForStructuredResponseAmongStartupMessages()
    {
        var script = Write("injector.py", "print('startup pid log')\nprint('{\"pid\":321,\"handle\":999}')\n");
        var psi = new ProcessStartInfo("/usr/bin/python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(script);
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new()));
        Assert.Equal(321, await runner.LaunchAsync(psi, ""));
    }

    [Fact]
    public async Task InjectorFailureIsNotReportedAsSuccessfulGameLaunch()
    {
        var script = Write("failed.py", "import sys\nprint('missing library',file=sys.stderr)\nsys.exit(7)\n");
        var psi = new ProcessStartInfo("/usr/bin/python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(script);
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new()));
        var error = await Assert.ThrowsAsync<IOException>(() => runner.LaunchAsync(psi, ""));
        Assert.Contains("missing library", error.Message);
        Assert.Contains("7", error.Message);
    }

    [Fact]
    public async Task CancelledLaunchNeverStartsTheGame()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var runner = new UnixDalamudRunner(new CompatibilityRunner(new()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.LaunchAsync(new ProcessStartInfo("/nonexistent"), "", cts.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
