using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using XIVLauncher.Linux.Platforms;
using XIVLauncher.Linux.Updates;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class PlatformUpdateTests
{
    private const string Page="https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases/tag/v3.0.0";
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {token.ThrowIfCancellationRequested();return Task.FromResult(send(request));} }
    // A fake alternate format proves transport is shared; no platform installer is involved.
    private sealed class AlternatePackage : IUpdatePackage
    {
        public string ManifestName=>"alternate-test-update.json";
        public string SignatureName=>"alternate-test-update.sig";
        public int Completed {get;private set;}
        public void ValidateTarget(string path) {if(!File.Exists(path))throw new IOException("Missing target");}
        public FileStream CreateStage(string path)=>new(path,FileMode.CreateNew,FileAccess.Write);
        public void CompleteStage(string path) {if(File.ReadAllBytes(path)[0]!=77)throw new IOException("Wrong format");Completed++;}
    }
    private static HttpResponseMessage Bytes(byte[] bytes)=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
    [Fact] public async Task SelectsOnlyTheRequestedPlatformAssets()
    {
        var package=new AlternatePackage();
        var names=new[]{"appimage-update.json","appimage-update.sig",package.ManifestName,package.SignatureName};
        var payload=JsonSerializer.SerializeToUtf8Bytes(new[]{new{tag_name="v3.0.0",draft=false,prerelease=false,html_url=Page,assets=names.Select(name=>new{name,browser_download_url=Page+"/"+name})}});
        using var http=new HttpClient(new Handler(_=>Bytes(payload)));
        var release=await new LauncherUpdates(http,new(),package).CheckAsync(default);
        Assert.Equal(Page+"/"+package.ManifestName,release!.Manifest);
        Assert.Equal(Page+"/"+package.SignatureName,release.Signature);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SharedSignatureVerificationAlsoProtectsOtherPlatforms(bool corrupt)
    {
        using var key=RSA.Create(2048);var package=new AlternatePackage();
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new SignedUpdate("3.0.0","stable",Page+"/package",new string('a',64),32));
        var sig=key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);if(corrupt)sig[0]^=1;
        using var http=new HttpClient(new Handler(r=>Bytes(r.RequestUri!.AbsolutePath.EndsWith(".sig")?sig:bytes)));
        var service=new LauncherUpdates(http,new(){PublicKey=key.ExportSubjectPublicKeyInfoPem()},package);
        var release=new LauncherRelease(new(3,0,0),Page,Page+"/"+package.ManifestName,Page+"/"+package.SignatureName);
        if(corrupt)await Assert.ThrowsAsync<IOException>(()=>service.VerifyAsync(release,default));
        else Assert.Equal("3.0.0",(await service.VerifyAsync(release,default)).Version);
    }
    [Theory][InlineData("valid")][InlineData("checksum")][InlineData("format")][InlineData("cancel")]
    public async Task SharedDownloadVerifiesAndCleansUpOtherFormats(string variant)
    {
        var root=Path.Combine(Path.GetTempPath(),"platform-update-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var target=Path.Combine(root,"launcher.fixture");File.WriteAllText(target,"old");var package=new AlternatePackage();
            var bytes=new byte[32];bytes[0]=variant=="format"?(byte)0:(byte)77;
            using var http=new HttpClient(new Handler(_=>Bytes(bytes)));
            var service=new LauncherUpdates(http,new(),package);var hash=Convert.ToHexString(SHA256.HashData(bytes));
            var update=new SignedUpdate("3.0.0","stable",Page+"/package",variant=="checksum"?new string('0',64):hash,32);
            using var cancel=new CancellationTokenSource();if(variant=="cancel")cancel.Cancel();
            if(variant=="valid")
            {var stage=await service.StageAsync(update,target,_=>{},cancel.Token);Assert.Equal(bytes,File.ReadAllBytes(stage));Assert.Equal(1,package.Completed);}
            else
            {await Assert.ThrowsAnyAsync<Exception>(()=>service.StageAsync(update,target,_=>{},cancel.Token));Assert.Single(Directory.GetFiles(root));Assert.Equal(0,package.Completed);}
            Assert.Equal("old",File.ReadAllText(target));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact] public void NativePlatformDoesNotAdvertiseOrAcceptCompatibilityOperations()
    {
        var native=new PlatformCapabilities(false);Assert.DoesNotContain("compatibility",native.Features);
        foreach(var method in new[]{"compatibility.read","compatibility.update","compatibility.diagnose"})Assert.Throws<NotSupportedException>(()=>native.RequireOperation(method));
        native.RequireOperation("preferences.read");Assert.Contains("compatibility",new PlatformCapabilities(true).Features);
    }
}
