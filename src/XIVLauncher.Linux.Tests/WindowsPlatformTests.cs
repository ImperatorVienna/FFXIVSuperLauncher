using System.Text;
using XIVLauncher.Dalamud;
using XIVLauncher.Linux.Platforms.Windows;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class WindowsPlatformTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "windows-adapter-" + Guid.NewGuid().ToString("N"));
    public WindowsPlatformTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    [Fact] public void PortableEntryKeepsPathsAsDataAndRefreshesAfterRename()
    {
        var executable = Path.Combine(root, "游戏 & launcher.exe"); File.WriteAllText(executable, "fixture");
        const string script = "$root = $env:FFXIV_LAUNCHER_ROOT; exit 0";
        WindowsPortableEntry.Write(executable, script);
        Assert.Equal(Path.GetFileName(executable), File.ReadAllText(Path.Combine(root, "launcher-executable.txt")));
        var batch = File.ReadAllText(Path.Combine(root, "launch.bat"));
        Assert.Contains("DisableDelayedExpansion", batch);
        Assert.DoesNotContain(root, batch);
        Assert.DoesNotContain("ExecutionPolicy", batch);
        Assert.Contains(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)), batch);
        var renamed = Path.Combine(root, "new.exe"); File.Move(executable, renamed);
        WindowsPortableEntry.Write(renamed, script);
        Assert.Equal("new.exe", File.ReadAllText(Path.Combine(root, "launcher-executable.txt")));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact] public void PortableEntryRefusesMissingExecutable()
    { Assert.Throws<IOException>(() => WindowsPortableEntry.Write(Path.Combine(root,"missing.exe"),"exit 0")); Assert.Empty(Directory.GetFiles(root)); }
    [Fact] public void WindowsStorageDoesNotInvokeUnixPermissions()
    {
        var storage = new WindowsStorage();
        Assert.Equal("windows-settings.json", storage.SettingsFileName);
        using(var file = storage.OpenPrivateFile(Path.Combine(root,"credentials"),FileMode.CreateNew,FileAccess.Write))file.WriteByte(42);
        Assert.Equal(new byte[]{42},File.ReadAllBytes(Path.Combine(root,"credentials")));
    }
    [Fact] public void NativeLaunchHasNoCompatibilityLayerOrShell()
    {
        var start = WindowsGameRuntime.BuildStartInfo("ffxiv", Path.Combine(root,"ffxiv_dx11.exe"),null,null,false,false);
        Assert.EndsWith("ffxiv_dx11.exe",start.FileName); Assert.False(start.UseShellExecute);
        Assert.Empty(start.ArgumentList); Assert.Equal(root,start.WorkingDirectory);
    }
    [Theory][InlineData("ffxiv")][InlineData("ffxiv_cn")][InlineData("ffxiv_tc")]
    public void NativeInjectionUsesNativePathsAndDoesNotApplyWineWorkarounds(string region)
    {
        var injector = new FileInfo(Path.Combine(root,"Dalamud.Injector.exe")); File.WriteAllText(injector.FullName,"fixture");
        var files = new RegionDalamudFiles(injector,new(root),new(root),1,region=="ffxiv_cn");
        var info = new DalamudStartInfo { ConfigurationPath=Path.Combine(root,"dalamud.json"), PluginDirectory=root, AssetDirectory=root,
            LauncherDirectory=root, LoggingPath=root, TroubleshootingPackData="{}" };
        var start = WindowsGameRuntime.BuildStartInfo(region,Path.Combine(root,"ffxiv_dx11.exe"),files,info,true,true);
        Assert.Equal(injector.FullName,start.FileName);
        Assert.Contains("--dalamud-configuration-path="+info.ConfigurationPath,start.ArgumentList);
        Assert.DoesNotContain("--no-fix-acl",start.ArgumentList);
        Assert.DoesNotContain("--dalamud-platform=linux",start.ArgumentList);
        Assert.Equal(root,start.Environment["DALAMUD_RUNTIME"]);
        Assert.Equal("--",start.ArgumentList.Last());
    }
    [Theory][InlineData("", "\"\"")][InlineData("a b", "\"a b\"")][InlineData("x\\", "\"x\\\\\"")]
    public void NativeArgumentsRemainSingleArguments(string input,string expected) => Assert.Equal(expected,WindowsCommandLine.Quote(input));
    [Theory][InlineData("/portable/launcher.AppImage", "/portable")][InlineData("/portable/launcher.exe", "/portable")][InlineData(null, "/unpacked")]
    public void PortableDataFollowsExecutableNotWorkingDirectory(string? executable, string expected)
    { Assert.Equal(expected, Platforms.PortablePaths.Resolve(executable, "/unpacked")); }
    [Fact] public void WindowsUpdateRejectsAnAppImage()
    {
        var path=Path.Combine(root,"fake.exe");File.WriteAllBytes(path,new byte[128]);
        Assert.ThrowsAny<Exception>(()=>new WindowsExecutablePackage().CompleteStage(path));
    }
}
