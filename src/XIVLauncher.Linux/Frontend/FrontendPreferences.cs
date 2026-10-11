using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux.Frontend;
internal sealed record PreferenceChange(string? Language=null,string? Region=null,Dictionary<string,string>? ClientPaths=null,
    string? CdKeyVersion=null,string? EuShopLanguage=null,int? ClientLanguage=null,bool? AutoCheckUpdates=null);
internal sealed class FrontendPreferences(string root, Action? ensureGameIdle = null)
{
    private readonly string root=Path.IsPathFullyQualified(root)?Path.GetFullPath(root):throw new ArgumentException("An absolute data directory is required.");
    internal object Snapshot()
    {
        var settings=LinuxSettings.Load(root);VersionRecords.Refresh(settings);return Snapshot(settings);
    }
    internal object Snapshot(LinuxSettings settings)
    {
        Localization.SetLanguage(settings.Language);
        return new
        {
            protocolVersion=5,launcherVersion=typeof(FrontendHost).Assembly.GetName().Version?.ToString(3),
            capabilities=new[]{"preferences.language","preferences.region","preferences.paths","compatibility","accounts","launch","game-update","official-news","official-browser","plugins","plugin-sync","dalamud-update","travel","setup","about","launcher-update"},
            isAppImage=new FrontendApplication(root).IsAppImage,setupComplete=settings.SetupComplete,autoCheckUpdates=settings.AutoCheckUpdates,language=settings.Language,selectedRegion=settings.SelectedRegion,protonScript=settings.ProtonScript,
            regions=LinuxSettings.Regions.Select(region=>
            {
                var available=settings.RegionProfiles.TryGetValue(region.Id,out var profile);
                return new
                {
                available,
                id=region.Id,gamePath=profile?.GamePath??"",clientVersion=profile?.ClientVersion??"",
                dalamudVersion=profile?.DalamudVersion??"",areaName=profile?.AreaName??"",
                cdKeyVersion=region.Id=="ffxiv"?profile?.CdKeyVersion:null,
                euShopLanguage=region.Id=="ffxiv"?profile?.EuShopLanguage:null,
                clientLanguage=region.Id=="ffxiv"?(int?)profile?.ClientLanguage:null
                };
            }).ToArray()
        };
    }
    internal object Update(PreferenceChange change)
    {
        if(change.Language is {} language && !Localization.Languages.Any(x=>x.Id==language))throw new ArgumentException("Unknown language.");
        if(change.Region is {} region)FrontendAccounts.Validate(region);
        if(change.ClientPaths is {} paths)
            foreach(var item in paths) {FrontendAccounts.Validate(item.Key);if(item.Value==null||item.Value.Length>4096||item.Value.Length>0&&!Path.IsPathFullyQualified(item.Value))throw new ArgumentException("Invalid client path.");}
        if(change.CdKeyVersion!=null && change.CdKeyVersion is not ("NA" or "JP" or "EU"))throw new ArgumentException("Unknown CDKey version.");
        if(change.EuShopLanguage!=null && !InternationalWeb.EuLanguages.Any(x=>x.Value==change.EuShopLanguage))throw new ArgumentException("Unknown language.");
        if(change.ClientLanguage is {} clientLanguage && !Enum.IsDefined((InternationalClientLanguage)clientLanguage))throw new ArgumentException("Unknown client language.");
        if(change==new PreferenceChange())throw new ArgumentException("Empty update.");
        if(change.ClientLanguage!=null||change.ClientPaths!=null)(ensureGameIdle ?? GameProcessGuard.EnsureGameSettingsIdle)();
        var settings=LinuxSettings.Load(root);
        if(change.AutoCheckUpdates is {} autoCheck)settings.AutoCheckUpdates=autoCheck;
        if(change.Language!=null)settings.Language=change.Language;
        if(change.Region!=null)settings.SelectedRegion=change.Region;
        if(change.ClientPaths!=null)foreach(var item in change.ClientPaths)settings.RegionProfiles[item.Key].GamePath=item.Value;
        var global=change.CdKeyVersion!=null||change.EuShopLanguage!=null||change.ClientLanguage!=null ? settings.RegionProfiles["ffxiv"] : null;
        if(change.CdKeyVersion!=null)global!.CdKeyVersion=change.CdKeyVersion;
        if(change.EuShopLanguage!=null)global!.EuShopLanguage=change.EuShopLanguage;
        if(change.ClientLanguage!=null)global!.ClientLanguage=(InternationalClientLanguage)change.ClientLanguage;
        if(change.ClientPaths!=null||change.CdKeyVersion!=null||change.EuShopLanguage!=null||change.ClientLanguage!=null)settings.Save();
        else settings.SaveGlobalPreferences();
        return Snapshot(settings);
    }
}
