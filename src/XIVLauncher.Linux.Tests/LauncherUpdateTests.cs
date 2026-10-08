using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XIVLauncher.Linux.Updates;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class LauncherUpdateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-update-" + Guid.NewGuid());
    private readonly RSA key = RSA.Create(2048);
    public void Dispose() { key.Dispose(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private UpdateDistribution Config(string channel="stable") => new() { Channel=channel, PublicKey=key.ExportSubjectPublicKeyInfoPem() };
    private const string Page="https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases/tag/v0.12.12";
    private static object Release(string version,bool preview=false,bool draft=false) => new { tag_name=version,prerelease=preview,draft,html_url=Page,assets=Array.Empty<object>() };
    [Theory][InlineData("stable","0.12.10")][InlineData("preview","1.0.0")]
    public async Task FiltersDraftsAndPreviewAndComparesNumericVersions(string channel,string expected)
    {
        using var http = new HttpClient(new Handler((_,_)=>Task.FromResult(Json(new[]{Release("0.12.9"),Release("0.12.10"),Release("1.0.0",true),Release("9.0.0",false,true),Release("junk")}))));
        Assert.Equal(Version.Parse(expected),(await new LauncherUpdates(http,Config(channel)).CheckAsync(default))!.Version);
    }
    [Theory][InlineData(404)][InlineData(200)]
    public async Task EmptyReleasesAreNormal(int code)
    {
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(code==404?new(HttpStatusCode.NotFound):Json(Array.Empty<object>()))));
        Assert.Null(await new LauncherUpdates(http,Config()).CheckAsync(default));
    }
    [Fact] public async Task CancellationDoesNotHang()
    {
        using var http=new HttpClient(new Handler(async (_,t)=>{await Task.Delay(Timeout.Infinite,t);return Json(0);}));
        using var cancel=new CancellationTokenSource(30);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new LauncherUpdates(http,Config()).CheckAsync(cancel.Token));
    }
    [Theory][InlineData("http://example.com/a")][InlineData("https://evil.invalid/a")][InlineData("http://127.0.0.1/a")]
    public void StableDoesNotAcceptOtherSources(string url) => Assert.Throws<IOException>(()=>Config().ValidateUrl(url));
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SignatureMustMatchUnmodifiedManifest(bool corrupt)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new SignedUpdate("0.12.12","stable",Page+"/image",new string('a',64),16));
        var signature=key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        if(corrupt) bytes[5]^=1;
        using var http=new HttpClient(new Handler((r,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(r.RequestUri!.AbsolutePath.EndsWith("sig")?signature:bytes)})));
        var service=new LauncherUpdates(http,Config());var release=new LauncherRelease(new(0,12,12),Page,Page+"/json",Page+"/sig");
        if(corrupt) await Assert.ThrowsAsync<IOException>(()=>service.VerifyAsync(release,default));
        else Assert.Equal("0.12.12",(await service.VerifyAsync(release,default)).Version);
    }
    [Theory][InlineData("valid")][InlineData("hash")][InlineData("size")][InlineData("format")][InlineData("cancel")]
    public async Task StageIsVerifiedBeforeOriginalIsTouched(string variant)
    {
        Directory.CreateDirectory(root);var original=Path.Combine(root,"launcher with spaces.AppImage");File.WriteAllText(original,"old");
        byte[] bytes=new byte[32];new byte[]{127,69,76,70}.CopyTo(bytes,0);bytes[8]=65;bytes[9]=73;bytes[10]=2;
        if(variant=="format") bytes[8]=0;
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)})));
        var service=new LauncherUpdates(http,Config());var update=new SignedUpdate("0.12.12","stable",Page+"/image",variant=="hash"?new string('0',64):hash,variant=="size"?33:32);
        using var cancel=new CancellationTokenSource();if(variant=="cancel") cancel.Cancel();
        if(variant=="valid") { var staged=await service.StageAsync(update,original,_=>{},cancel.Token);Assert.Equal(bytes,File.ReadAllBytes(staged)); }
        else {await Assert.ThrowsAnyAsync<Exception>(()=>service.StageAsync(update,original,_=>{},cancel.Token));Assert.Single(Directory.GetFiles(root));}
        Assert.Equal("old",File.ReadAllText(original));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task InstallerSwitchesVersionedFileAndEntryOrRefusesTampering(bool valid)
    {
        Directory.CreateDirectory(root);var image=Path.Combine(root,"file ' with spaces.AppImage");var stage=image+".new";
        var payload="#!/bin/sh\n[ -z \"${APPIMAGE:-}\" ] && [ -z \"${APPDIR:-}\" ] || exit 9\nprintf restarted > \"$PWD/restarted\"\n";
        File.WriteAllText(image,"old");File.WriteAllText(stage,payload);
        string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        var destination=Path.Combine(root,"xivlauncher-super-0.12.14-x86_64.AppImage");
        var entry=AppImageEntry.Refresh(image,root);var entryStage=entry+".new";File.WriteAllText(entryStage,AppImageEntry.Script(destination));
        var start=new System.Diagnostics.ProcessStartInfo("/bin/sh"){UseShellExecute=false};
        foreach(var arg in new[]{"-c",UpdateInstallation.InstallScript,"test",image,stage,Hash(valid?payload:"wrong"),Hash("old"),"2147483647",destination,entry,entryStage})start.ArgumentList.Add(arg);
        start.Environment["APPIMAGE"]="/old/mount/image";start.Environment["APPDIR"]="/old/mount";
        using var process=System.Diagnostics.Process.Start(start)!;await process.WaitForExitAsync();
        Assert.Equal(valid?0:1,process.ExitCode);Assert.Equal(valid?payload:"old",File.ReadAllText(valid?destination:image));
        Assert.Equal(valid,File.Exists(Path.Combine(root,"restarted")));
        Assert.Equal(!valid,File.Exists(image));Assert.Equal(AppImageEntry.Script(valid?destination:image),File.ReadAllText(entry));
        if(valid)Assert.Equal("old",File.ReadAllText(image+".previous"));
    }
    [Fact] public void AppImageDesktopEntryUsesPersistentPathAndRequestedDescription()
    {
        Directory.CreateDirectory(root);var image=Path.Combine(root,"launcher with spaces.AppImage");File.WriteAllText(image,"fixture");
        var icon=Path.Combine(root,"icon.png");File.WriteAllText(icon,"icon");
        DesktopIntegration.Install(image,root,icon);
        var entry=File.ReadAllText(Path.Combine(root,"applications/xivlauncher-super.desktop"));
        Assert.Contains("Exec=\""+image+"\"",entry);
        Assert.Contains("It supports the China, Traditional Chinese, and Global game clients.",entry);
        Assert.DoesNotContain(".mount_",entry);
        var installedIcon = entry.Split('\n').Single(line => line.StartsWith("Icon="))[5..];
        Assert.True(Path.IsPathFullyQualified(installedIcon));
        Assert.Equal(File.ReadAllBytes(icon), File.ReadAllBytes(installedIcon));
        File.Delete(icon); // Desktop integration must survive AppImage unmounting.
        Assert.True(File.Exists(installedIcon));
    }
    [Fact] public void EntryCreatesInitialConfigurationWithoutOverwritingExistingPreferences()
    {
        Directory.CreateDirectory(root);var image=Path.Combine(root,"launcher.AppImage");File.WriteAllText(image,"fixture");
        AppImageEntry.Refresh(image,root);
        Assert.False(LinuxSettings.Load(root).SetupComplete);
        var settings=LinuxSettings.Load(root);settings.SetupComplete=true;settings.Save();
        var path=Path.Combine(root,"linux-settings.json");var previous=File.ReadAllBytes(path);
        AppImageEntry.Refresh(image,root);Assert.Equal(previous,File.ReadAllBytes(path));
    }
    [Fact] public async Task StableEntryFollowsMovedImageAndForwardsArguments()
    {
        Directory.CreateDirectory(root);
        var image=Path.Combine(root,"first ' image.AppImage");
        File.WriteAllText(image,"#!/bin/sh\nprintf '%s\\n' \"$@\"\n");
        File.SetUnixFileMode(image,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var entry=AppImageEntry.Refresh(image,root);
        var moved=Path.Combine(root,"new location.AppImage");File.Move(image,moved);
        Assert.Equal(entry,AppImageEntry.Refresh(moved,root));
        var start=new System.Diagnostics.ProcessStartInfo(entry){UseShellExecute=false,RedirectStandardOutput=true};start.ArgumentList.Add("--steam-entry");
        using var process=System.Diagnostics.Process.Start(start)!;
        Assert.Equal("--steam-entry",(await process.StandardOutput.ReadToEndAsync()).Trim());await process.WaitForExitAsync();Assert.Equal(0,process.ExitCode);
    }
    [Fact] public void StartupPreparesConfigurationEntryAndDesktopBeforeUpdates()
    {
        Directory.CreateDirectory(root);var image=Path.Combine(root,"launcher.AppImage");File.WriteAllText(image,"fixture");
        var icon=Path.Combine(root,"icon.png");File.WriteAllText(icon,"icon");
        DesktopIntegration.PrepareAppImageStartup(image,root,icon);
        var config=Path.Combine(root,"xivlauncher-super");
        Assert.False(LinuxSettings.Load(config).SetupComplete);
        Assert.True(File.Exists(Path.Combine(config,"linux-settings.json")));
        var entry=Path.Combine(config,"appimage-launcher");Assert.True(File.Exists(entry));
        Assert.Contains(entry,File.ReadAllText(Path.Combine(root,"applications/xivlauncher-super.desktop")));
        var previous=File.ReadAllBytes(Path.Combine(config,"linux-settings.json"));
        DesktopIntegration.PrepareAppImageStartup(image,root,icon);
        Assert.Equal(previous,File.ReadAllBytes(Path.Combine(config,"linux-settings.json")));
    }

    [Fact] public async Task DesktopCacheRefreshHandlesOutputWithoutATerminal()
    {
        await DesktopIntegration.RunCacheCommandAsync("/bin/sh", ["-c", "printf output; printf warning >&2"], TimeSpan.FromSeconds(2));
    }
    [Fact] public async Task DesktopCacheRefreshReportsFailure()
    {
        await Assert.ThrowsAsync<IOException>(() => DesktopIntegration.RunCacheCommandAsync("/bin/sh", ["-c", "exit 7"], TimeSpan.FromSeconds(2)));
    }
    [Fact] public async Task DesktopCacheRefreshTimesOut()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DesktopIntegration.RunCacheCommandAsync("/bin/sh", ["-c", "sleep 30"], TimeSpan.FromMilliseconds(100)));
    }
    [Fact] public void StageRejectsSymlinkWithoutTouchingTarget()
    {
        Directory.CreateDirectory(root);var image=Path.Combine(root,"real");File.WriteAllText(image,"old");var link=Path.Combine(root,"link");File.CreateSymbolicLink(link,image);
        Assert.Throws<IOException>(()=>LauncherUpdates.ValidateTarget(link));Assert.Equal("old",File.ReadAllText(image));
    }
}
