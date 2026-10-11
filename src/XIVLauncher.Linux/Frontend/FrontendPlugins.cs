using System.Reflection;
using Newtonsoft.Json.Linq;
using XIVLauncher.Common.Http;

namespace XIVLauncher.Linux.Frontend;

internal sealed record PluginSourceRequest(string Content);
internal sealed record RepositoryChange(string Region, string Revision, PluginRepositoryEntry[] Entries, bool PresetsEnabled);
internal sealed record PluginActionRequest(string Region, string Kind, string? Revision = null,
    Dictionary<string, bool>? States = null, string? PlanId = null, string[]? Plugins = null,
    string? Content = null, string? Source = null, string[]? Targets = null, string? Url = null);
internal sealed record PluginEntry(string Id, string Name, string Version, bool? Enabled, bool ScheduledForDeletion);
internal sealed record PluginPageSnapshot(string Region, string Revision, string DalamudVersion, PluginEntry[] Plugins, int ProblemCount);
internal sealed record PluginPlanEntry(string Id, string Name, string FromVersion, string ToVersion, string Repository);
internal sealed record PluginPlanView(string Region, string PlanId, PluginPlanEntry[] Updates, string[] Problems);

// DTOs contain identities only. Paths, installed manifests and update plans stay in the backend.
internal sealed class FrontendPlugins(string root, PluginUpdates? updater = null, Action? ensureIdle = null,
    Func<LinuxSettings, Action<string>, CancellationToken, Task>? updateDalamud = null)
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) }) { Timeout = TimeSpan.FromSeconds(90) };
    private readonly PluginUpdates updates = updater ?? new PluginUpdates(Http);
    private readonly Action ensureIdle = ensureIdle ?? GameProcessGuard.EnsureGamesStopped;
    private readonly Dictionary<string, (string Revision, OfflinePluginList List)> lists = new();
    private readonly Dictionary<string, (string Id, PluginUpdatePlan Plan)> plans = new();

    internal PluginPageSnapshot Read(string region)
    {
        FrontendAccounts.Validate(region);
        lists.Remove(region);
        var list = OfflinePluginManager.Read(root, region);
        if (list.Plugins.Count > 1000) throw new IOException("Plugin list exceeds the frontend limit.");
        var revision = Guid.NewGuid().ToString("N");
        var settings = LinuxSettings.Load(root); VersionRecords.Refresh(settings);
        lists[region] = (revision, list);
        return new(region, revision, settings.RegionProfiles[region].DalamudVersion,
            list.Plugins.Select(p => new PluginEntry(p.InternalName, p.Name, p.Version, p.Enabled, p.ScheduledForDeletion)).ToArray(), list.Problems.Count);
    }
    internal PluginRepositorySnapshot Repositories(string region) => PluginRepositories.Read(root, region);
    internal PluginRepositorySnapshot SaveRepositories(RepositoryChange request)
    {
        FrontendAccounts.Validate(request.Region);
        if (LinuxSettings.Load(root).SelectedRegion != request.Region) throw new ArgumentException("The selected region has changed.");
        var result = PluginRepositories.Save(root, request.Region, request.Revision, request.Entries, request.PresetsEnabled, ensureIdle);
        plans.Clear(); return result;
    }
    internal object Sources(PluginSourceRequest request)
    {
        var content = ParseContent(request.Content);
        return LinuxSettings.Regions.Select(r => new { region = r.Id, hasSource = RegionDataSync.HasSource(root, r.Id, content) }).ToArray();
    }
    private static RegionSyncContent ParseContent(string? value) => value switch
    {
        "third-party-repositories" => RegionSyncContent.ThirdPartyRepositories,
        "installed-plugins" => RegionSyncContent.InstalledPlugins,
        "plugin-settings" => RegionSyncContent.PluginSettings,
        _ => throw new ArgumentException("Unknown synchronization category.")
    };
    private void Validate(PluginActionRequest request)
    {
        FrontendAccounts.Validate(request.Region);
        if (LinuxSettings.Load(root).SelectedRegion != request.Region) throw new ArgumentException("The selected region has changed.");
        bool invalid = request.Kind switch
        {
            "apply" => request.Revision == null || request.States == null || request.States.Count is 0 or > 1000,
            "install-updates" => request.PlanId == null || request.Plugins == null || request.Plugins.Length is 0 or > 1000,
            "sync" => request.Source == null || request.Targets == null || request.Targets.Length is 0 or > 2 || request.Content == null,
            "check-repository" => string.IsNullOrWhiteSpace(request.Url),
            "check-updates" or "dalamud-update" => false,
            _ => true
        };
        if (request.Kind != "apply" && (request.States != null || request.Revision != null) ||
            request.Kind != "install-updates" && (request.PlanId != null || request.Plugins != null) ||
            request.Kind != "sync" && (request.Content != null || request.Source != null || request.Targets != null)) invalid = true;
        if (request.Kind != "check-repository" && request.Url != null) invalid = true;
        if (invalid) throw new ArgumentException("Invalid plugin operation.");
        if (request.Kind == "check-repository") _ = PluginUpdates.Url(request.Url!);
        if (request.Kind == "sync")
        {
            _ = ParseContent(request.Content); FrontendAccounts.Validate(request.Source!);
            foreach (var target in request.Targets!) FrontendAccounts.Validate(target);
            if (request.Targets.Contains(request.Source!) || request.Targets.Distinct().Count() != request.Targets.Length)
                throw new ArgumentException("Select unique destination regions excluding the source.");
        }
    }
    internal object Start(PluginActionRequest request, FrontendOperations operations)
    {
        operations.EnsureIdle(); Validate(request);
        OfflinePluginList? displayed = null; PluginUpdatePlan? plan = null;
        if (request.Kind == "apply")
        {
            if (!lists.TryGetValue(request.Region, out var cached) || cached.Revision != request.Revision)
                throw new CredentialValidationException("Stale plugin list.", "插件列表已过期，请刷新后重试。");
            displayed = cached.List;
            if (request.States!.Keys.Any(name => !displayed.Plugins.Any(p => p.InternalName == name && !p.ScheduledForDeletion)))
                throw new ArgumentException("Unknown or scheduled-for-deletion plugin.");
        }
        if (request.Kind == "install-updates")
        {
            if (!plans.TryGetValue(request.Region, out var cached) || cached.Id != request.PlanId)
                throw new CredentialValidationException("Stale plugin update plan.", "更新列表已过期，请重新检查插件更新。");
            plan = cached.Plan;
            if (request.Plugins!.Distinct().Count() != request.Plugins.Length || request.Plugins.Any(name => !plan.Updates.Any(p => p.Plugin.InternalName == name)))
                throw new ArgumentException("Unknown plugin update.");
        }
        return operations.Start(request.Region, async operation =>
        {
            if (request.Kind != "check-repository") ensureIdle();
            operation.Token.ThrowIfCancellationRequested();
            // A plan is single use; a partial update must be checked again, never replayed.
            plans.Clear();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
            deadline.CancelAfter(request.Kind is "check-updates" or "check-repository" ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(30));
            var token = deadline.Token;
            using var downloads = new DownloadScope(operation.Download, token);
            try
            {
                switch (request.Kind)
                {
                    case "check-repository":
                        var catalog = Newtonsoft.Json.Linq.JArray.Parse(await Http.GetStringAsync(PluginUpdates.Url(request.Url!), token));
                        if (catalog.Any(x => x is not Newtonsoft.Json.Linq.JObject || string.IsNullOrWhiteSpace(x.Value<string>("InternalName"))))
                            throw new IOException("The repository does not contain a valid plugin catalog.");
                        operation.SetResult(new { kind = "repository-checked", count = catalog.Count });
                        operation.Status($"Repository is accessible: {catalog.Count} plugins.");
                        break;
                    case "apply":
                        operation.Status("Applying plugin enablement states.");
                        var backup = OfflinePluginManager.ApplySelection(root, request.Region, displayed!, request.States!);
                        lists.Remove(request.Region);
                        operation.SetResult(new { kind = "plugins-applied", backupDirectory = backup });
                        operation.Status("Plugin enablement states saved. They take effect on the next game launch.");
                        break;
                    case "dalamud-update":
                        var settings = LinuxSettings.Load(root);
                        operation.Status("Checking Dalamud updates.");
                        if (updateDalamud != null) await updateDalamud(settings, operation.Status, token);
                        else await new RegionDalamudUpdates().UpdateAsync(settings, operation.Status, token, manual: true);
                        lists.Remove(request.Region);
                        operation.Status("Dalamud update completed.");
                        break;
                    case "check-updates":
                        operation.Status("Checking installed plugins in their original repositories.");
                        var current = LinuxSettings.Load(root);
                        var assembly = VersionRecords.DalamudAssembly(root, request.Region, current.RegionProfiles[request.Region].DalamudVersion)
                            ?? throw new CredentialValidationException("No installed Dalamud.", "请先安装或更新 Dalamud，再检查插件更新。");
                        var version = AssemblyName.GetAssemblyName(assembly).Version ?? throw new IOException("Invalid Dalamud assembly version.");
                        var found = await updates.CheckAsync(root, request.Region, version, token);
                        var id = Guid.NewGuid().ToString("N"); plans[request.Region] = (id, found);
                        operation.SetResult(new { kind = "plugin-updates", plan = new PluginPlanView(request.Region, id,
                            found.Updates.Select(p => new PluginPlanEntry(p.Plugin.InternalName, p.Plugin.Name, p.Plugin.Version, p.Version.ToString(), p.Repository.GetLeftPart(UriPartial.Path))).ToArray(),
                            found.Problems.Take(100).Select(UserLogMessage.Compact).ToArray()) });
                        operation.Status($"Plugin update check completed: {found.Updates.Count} updates; {found.Problems.Count} warnings.");
                        break;
                    case "install-updates":
                        foreach (var name in request.Plugins!)
                        {
                            token.ThrowIfCancellationRequested();
                            var update = plan!.Updates.Single(p => p.Plugin.InternalName == name);
                            await updates.InstallAsync(plan, update, ensureIdle, token);
                            operation.Status("Plugin updated: " + name);
                        }
                        lists.Remove(request.Region);
                        operation.Status("Selected plugin updates completed.");
                        break;
                    case "sync":
                        operation.Status("Synchronizing the selected data category and destination regions.");
                        var result = RegionDataSync.SynchronizeCore(root, request.Source!, request.Targets!, ParseContent(request.Content), token, _ => ensureIdle());
                        lists.Clear();
                        operation.SetResult(new { kind = "plugins-synced", result.BackupDirectory, result.Targets });
                        operation.Status("Synchronization completed. A backup of the original destination data was retained.");
                        break;
                }
            }
            catch (OperationCanceledException ex) when (!operation.Token.IsCancellationRequested)
            { throw new TimeoutException("The plugin operation timed out.", ex); }
        });
    }
}
