using XIVLauncher.Linux.Patching;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XIVLauncher.Linux;
using XIVLauncher.Linux.Taiwan;
using XIVLauncher.Linux.Patching.ZiPatch;
using XIVLauncher.Linux.Patching.ZiPatch.Util;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class TaiwanTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-tc-test-" + Guid.NewGuid().ToString("N"));
    private const string Version = "2025.10.27.0000.0000";
    public TaiwanTests() { Directory.CreateDirectory(Path.Combine(root, "game/sqpack/ex1")); File.WriteAllText(Path.Combine(root, "game/ffxivgame.ver"), Version); File.WriteAllText(Path.Combine(root, "game/sqpack/ex1/ex1.ver"), Version); }
    [Fact] public void WindowsRuntimeOverridesInheritedLinuxArchitectureRuntime()
    {
        var runner = new XIVLauncher.Common.Unix.CompatibilityRunner(new() { Executable = "/fake/proton", DataDirectory = root, SteamRoot = root });
        var psi = runner.BuildStartInfo(["injector.exe"], environment: new Dictionary<string, string> { ["DOTNET_ROOT"] = @"Z:\windows-runtime" });
        Assert.Equal(@"Z:\windows-runtime", psi.Environment["DOTNET_ROOT_X64"]);
    }
    [Fact] public void RegionIdsAndCredentialDefaults()
    {
        var settings = LinuxSettings.Load(root); Assert.Equal("ffxiv_cn", settings.SelectedRegion);
        Assert.Equal(new[] { "ffxiv_cn", "ffxiv_tc", "ffxiv" }, LinuxSettings.Regions.Select(r => r.Id));
        Assert.All(settings.RegionProfiles.Values, p => Assert.Empty(p.Accounts));
        settings.SelectedRegion = "ffxiv_tc"; settings.EnsureRegionAvailable();
        settings.Save(); Assert.True(File.Exists(Path.Combine(root, "ffxiv_tc/settings.json"))); Assert.False(Directory.Exists(Path.Combine(root, "ffxiv_sc")));
    }
    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void TotpMatchesRfc6238(long seconds, string expected) => Assert.Equal(expected, OneTimePassword.Generate("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", DateTimeOffset.FromUnixTimeSeconds(seconds)));
    [Fact] public void CredentialEncodingMatchesUpstreamWebLoginBridge() => Assert.Equal("416e2c", TaiwanLogin.EncodeCredential("A測"));
    [Fact] public void OtpRejectsOneTimeCodeAsSecret() => Assert.Throws<ArgumentException>(() => OneTimePassword.Decode("123456"));
    [Fact] public async Task PlainCredentialsAreRegionAccountScopedAndPrivate()
    {
        var settings = LinuxSettings.Load(root); 
        await RegionCredentials.StoreAsync(settings, "account", "password", "fixture-password", default);
        await RegionCredentials.StoreAsync(settings, "other", "otp", "fixture-secret", default);
        Assert.Equal("fixture-password", await RegionCredentials.ReadAsync(settings, "account", "password", default));
        var path = Path.Combine(root, "ffxiv_cn/credentials.json");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        settings.SelectedRegion = "ffxiv_tc"; 
        Assert.Null(await RegionCredentials.ReadAsync(settings, "account", "password", default));
        await RegionCredentials.StoreAsync(settings, "account", "password", "taiwan-password", default);
        await RegionCredentials.ClearAsync(settings, "account", default);
        settings.SelectedRegion = "ffxiv_cn"; Assert.Equal("fixture-password", await RegionCredentials.ReadAsync(settings, "account", "password", default));
        Assert.Equal("fixture-secret", await RegionCredentials.ReadAsync(settings, "other", "otp", default));
    }
    [Fact] public async Task MalformedCredentialFileNeverOverwritten()
    {
        var s = LinuxSettings.Load(root);  s.Save(); var path = Path.Combine(root, "ffxiv_cn/credentials.json"); File.WriteAllText(path, "broken");
        await Assert.ThrowsAsync<JsonException>(() => RegionCredentials.StoreAsync(s, "account", "password", "secret", default)); Assert.Equal("broken", File.ReadAllText(path));
    }
    [Fact] public async Task LoginSequenceRequestsSessionWithoutRedundantPatchHandshake()
    {
        var calls = new List<string>();
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.Contains("Orbit/1.0", request.Headers.UserAgent.ToString());
            Assert.Equal("en-US, en", request.Headers.AcceptLanguage.ToString());
            Assert.NotNull(request.Content!.Headers.ContentLength);
            calls.Add(request.RequestUri!.Host + request.RequestUri.AbsolutePath);
            var body = await request.Content!.ReadAsStringAsync();
            if (calls.Count == 1)
            {
                using var json = JsonDocument.Parse(body); Assert.Equal("613140622e636f6d", json.RootElement.GetProperty("email").GetString()); Assert.Equal("123456", json.RootElement.GetProperty("code").GetString());
                return Response("{\"token\":\"fixture-token\"}");
            }
            Assert.Contains("fixture-token", body); return Response("{\"sessionId\":\"fixture-session\"}");
        }));
        var result = await new TaiwanLogin(client).LoginAsync(root, "a1@b.com", "password", "123456", "captcha", default);
        Assert.Equal("fixture-session", result); Assert.Equal(2, calls.Count); Assert.Contains("launcherLogin", calls[0]); Assert.Contains("launcherSession", calls[1]);
        Assert.Contains("neolobby01.ffxiv.com.tw", TaiwanLogin.GameArguments(result));
    }
    [Fact] public async Task LoginErrorDoesNotExposeServerBody()
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("secret-token-and-password") })));
        var error = await Assert.ThrowsAsync<TaiwanAuthenticationException>(() => new TaiwanLogin(client).LoginAsync(root, "mail", "password", "", "captcha", default)); Assert.DoesNotContain("secret-token", error.Message);
    }
    [Fact] public void PatchManifestUsesTaiwanSourceAndChecksHashes()
    {
        var hash = Convert.ToHexString(SHA1.HashData("data"u8));
        var line = $"4\t4\t1\t1\t{Version}\tsha1\t4\t{hash}\thttp://patch-dl.ffxiv.com.tw/game/ex1/repo/D{Version}.patch";
        var patch = Assert.Single(TaiwanGameUpdater.Parse("--boundary\nContent-Type: text/plain\n" + line)); Assert.Equal(1, patch.Repository);
        Assert.Throws<IOException>(() => TaiwanGameUpdater.Parse(line.Replace("patch-dl.ffxiv.com.tw", "example.com")));
        Assert.Throws<IOException>(() => TaiwanGameUpdater.Parse(line.Replace(hash, "bad")));
    }
    [Fact] public async Task TamperedPatchIsRejectedBeforeApply()
    {
        var file = Path.Combine(root, "fixture.patch"); await File.WriteAllTextAsync(file, "bad!");
        var patch = new GamePatch(4, Version, 0, 4, [Convert.ToHexString(SHA1.HashData("data"u8))], new Uri("http://patch-dl.ffxiv.com.tw/file"));
        await Assert.ThrowsAsync<IOException>(() => TaiwanGameUpdater.VerifyAsync(file, patch, default));
    }
    [Fact] public async Task VersionErrorsAreNotTreatedAsUpToDate()
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(Response("<html>server unavailable</html>"))));
        await Assert.ThrowsAsync<IOException>(() => new TaiwanGameUpdater(client).CheckAsync(root, default));
    }
    [Fact] public void PatchPathsStayInGameDirectory()
    {
        Assert.EndsWith("game/sqpack/file.dat", SqexFile.Resolve(Path.Combine(root, "game"), "sqpack\\file.dat"));
        Assert.Throws<IOException>(() => SqexFile.Resolve(Path.Combine(root, "game"), "../../outside"));
    }
    [Fact] public void ZipatchReaderHandlesMinimalFileAndRejectsTruncation()
    {
        var bytes = Convert.FromHexString("915A4950415443480D0A1A0A00000000454F465F00000000");
        using var patch = new ZiPatchFile(new MemoryStream(bytes)); Assert.Equal("EOF_", Assert.Single(patch.GetChunks()).ChunkType);
        using var truncated = new ZiPatchFile(new MemoryStream(bytes[..^1])); Assert.Throws<ZiPatchException>(() => truncated.GetChunks().ToArray());
    }
    [Fact] public async Task DisabledDalamudDoesNotEvenResolveUpdateBackend()
    {
        Assert.Null(await new RegionDalamudUpdates((_, _, _, _) => throw new Exception("must not update")).UpdateAsync(LinuxSettings.Load(root), _ => { }, default));
        Assert.False(Directory.Exists(Path.Combine(root, "addon")));
    }
    [Fact] public async Task TaiwanUpdateAppliesVerifiedPatchThenWritesVersion()
    {
        var bytes = Convert.FromHexString("915A4950415443480D0A1A0A00000000454F465F00000000");
        var next = "2026.10.03.0000.0000";
        var manifest = $"{bytes.Length}\t{bytes.Length}\t1\t1\t{next}\tsha1\t100\t{Convert.ToHexString(SHA1.HashData(bytes))}\thttp://patch-dl.ffxiv.com.tw/game/repo/D{next}.patch";
        var checks = 0;
        using var client = new HttpClient(new Handler(request => Task.FromResult(request.Method == HttpMethod.Post
            ? ++checks == 1 ? Response(manifest) : new HttpResponseMessage(HttpStatusCode.NoContent)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })));
        await new TaiwanGameUpdater(client).UpdateAsync(root, Path.Combine(root, "ffxiv_tc"), _ => { }, default);
        Assert.Equal(next, File.ReadAllText(Path.Combine(root, "game/ffxivgame.ver")));
        Assert.Equal(Version, File.ReadAllText(Path.Combine(root, "game/sqpack/ex1/ex1.ver")));
    }
    [Fact] public async Task PlainLaunchReportsEarlyFailureWithoutAnyInjector()
    {
        var psi = new System.Diagnostics.ProcessStartInfo("/bin/false") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        await Assert.ThrowsAsync<IOException>(() => RegionLaunchPreparation.LaunchPlainAsync(psi, "", default));
    }
    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => response(request); }
    public void Dispose() => Directory.Delete(root, true);
}
