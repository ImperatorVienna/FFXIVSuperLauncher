using System.Net;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class PluginUpdateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-plugin-update-" + Guid.NewGuid());
    private const string Repo = "https://repo.test/master.json";
    private readonly Guid id = Guid.NewGuid();
    private string Installed(string region = "ffxiv_cn", string source = Repo)
    {
        var dir = Path.Combine(root, region, "dalamud", "installedPlugins", "Example", "1.0.0.0"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Example.dll"), "old");
        File.WriteAllText(Path.Combine(dir, "Example.json"), new JObject { ["InternalName"] = "Example", ["AssemblyVersion"] = "1.0.0.0", ["InstalledFromUrl"] = source, ["WorkingPluginId"] = id.ToString(), ["Disabled"] = true }.ToString());
        File.WriteAllText(Path.Combine(root, region, "dalamud", "dalamudConfig.json"), new JObject {
            ["MainRepoUrl"] = "https://main.test/master.json", ["ThirdRepoList"] = new JArray(new JObject { ["Url"] = Repo, ["IsEnabled"] = true }, new JObject { ["Url"] = "https://other.test/master.json", ["IsEnabled"] = true }) }.ToString());
        return dir;
    }
    private static JObject Manifest(string version = "2.0.0.0") => new() { ["InternalName"] = "Example", ["Name"] = "Example", ["AssemblyVersion"] = version, ["DalamudApiLevel"] = 17, ["DownloadLinkInstall"] = "https://repo.test/plugin.zip" };
    private static byte[] Zip(bool invalid = false, bool traversal = false)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("Example.json").Open())) writer.Write(Manifest(invalid ? "9.0.0.0" : "2.0.0.0"));
            using (var writer = new StreamWriter(zip.CreateEntry("Example.dll").Open())) writer.Write("new");
            if (traversal) using (var writer = new StreamWriter(zip.CreateEntry("../outside").Open())) writer.Write("bad");
        }
        return stream.ToArray();
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(send(request)); } }
    private static HttpResponseMessage Json(JToken json) => new(HttpStatusCode.OK) { Content = new StringContent(json.ToString()) };
    [Theory][InlineData("ffxiv_cn")][InlineData("ffxiv_tc")][InlineData("ffxiv")]
    public async Task OriginalRepositoryOnlyAndInstallPreservesIdentitySettingsAndOldVersion(string region)
    {
        var old = Installed(region); var requests = new List<string>();
        using var client = new HttpClient(new Handler(request => { requests.Add(request.RequestUri!.AbsoluteUri); return request.RequestUri.AbsolutePath.EndsWith("zip") ? new(HttpStatusCode.OK) { Content = new ByteArrayContent(Zip()) } : Json(new JArray(Manifest())); }));
        var updater = new PluginUpdates(client); var plan = await updater.CheckAsync(root, region, new(17, 0), default); var update = Assert.Single(plan.Updates);
        Assert.Equal(Repo, Assert.Single(requests)); Assert.Empty(plan.Problems);
        var config = File.ReadAllText(Path.Combine(root, region, "dalamud/dalamudConfig.json")); var idle = 0;
        await updater.InstallAsync(plan, update, () => idle++, default);
        var next = JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(old)!, "2.0.0.0/Example.json")));
        Assert.Equal(id, Guid.Parse(next.Value<string>("WorkingPluginId")!)); Assert.Equal(Repo, next.Value<string>("InstalledFromUrl")); Assert.True(next.Value<bool>("Disabled"));
        Assert.Equal(config, File.ReadAllText(Path.Combine(root, region, "dalamud/dalamudConfig.json"))); Assert.True(File.Exists(Path.Combine(old, "Example.dll"))); Assert.Equal(1, idle);
        Assert.Equal("2.0.0.0", Assert.Single(OfflinePluginManager.Read(root, region).Plugins).Version);
        foreach (var other in LinuxSettings.Regions.Where(x => x.Id != region)) Assert.False(Directory.Exists(Path.Combine(root, other.Id)));
    }
    [Theory][InlineData("")][InlineData("https://unknown.test/master.json")]
    public async Task MissingOrUnknownSourceNeverFallsBack(string source)
    {
        Installed(source: source); using var client = new HttpClient(new Handler(_ => throw new Exception("No request expected")));
        var plan = await new PluginUpdates(client).CheckAsync(root, "ffxiv_cn", new(17, 0), default); Assert.Empty(plan.Updates); Assert.Single(plan.Problems);
    }
    [Fact] public async Task OfficialMarkerOnlyUsesMainRepository()
    {
        Installed(source: "OFFICIAL"); using var client = new HttpClient(new Handler(request => { Assert.Equal("main.test", request.RequestUri!.Host); return Json(new JArray(Manifest())); }));
        Assert.Single((await new PluginUpdates(client).CheckAsync(root, "ffxiv_cn", new(17, 0), default)).Updates);
    }
    [Theory][InlineData("same")][InlineData("duplicate")][InlineData("api")][InlineData("unavailable")]
    public async Task UnsafeOrUnavailableUpdatesAreNotOffered(string variant)
    {
        Installed(); using var client = new HttpClient(new Handler(_ =>
        {
            if (variant == "unavailable") return new(HttpStatusCode.ServiceUnavailable);
            var manifest = Manifest(variant == "same" ? "1.0.0.0" : "2.0.0.0"); if (variant == "api") manifest["DalamudApiLevel"] = 18;
            return Json(variant == "duplicate" ? new JArray(manifest, manifest.DeepClone()) : new JArray(manifest));
        }));
        var plan = await new PluginUpdates(client).CheckAsync(root, "ffxiv_cn", new(17, 0), default); Assert.Empty(plan.Updates);
        if (variant == "same") Assert.Empty(plan.Problems); else Assert.Single(plan.Problems);
    }
    [Theory][InlineData("version")][InlineData("traversal")][InlineData("changed")][InlineData("running")][InlineData("download")]
    public async Task FailedInstallLeavesOldPluginIntact(string failure)
    {
        var old = Installed(); using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("zip")
            ? failure == "download" ? new(HttpStatusCode.ServiceUnavailable) : new(HttpStatusCode.OK) { Content = new ByteArrayContent(Zip(failure == "version", failure == "traversal")) } : Json(new JArray(Manifest()))));
        var updater = new PluginUpdates(client); var plan = await updater.CheckAsync(root, "ffxiv_cn", new(17, 0), default);
        if (failure == "changed") File.AppendAllText(Path.Combine(root, "ffxiv_cn/dalamud/dalamudConfig.json"), " ");
        await Assert.ThrowsAnyAsync<Exception>(() => updater.InstallAsync(plan, Assert.Single(plan.Updates), () => { if (failure == "running") throw new IOException("Game running"); }, default));
        Assert.Equal("old", File.ReadAllText(Path.Combine(old, "Example.dll"))); Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(old)!, "2.0.0.0")));
        Assert.Empty(Directory.GetDirectories(root, ".plugin-update-*"));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
