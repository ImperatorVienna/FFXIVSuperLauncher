using XIVLauncher.Common.Unix;

namespace XIVLauncher.Linux.Frontend;

internal sealed record CompatibilityChange(string? ProtonScript = null, string? SteamRoot = null,
    string? DataDirectory = null, bool? Registered = null, bool? UseSteamPrefix = null, string? SteamPrefix = null);
internal sealed record RuntimeEntry(string Name, string Script, bool Ready, string? RequiredRuntime, string SteamRoot);
internal sealed record PrefixEntry(string Name, string Path);
internal sealed record CompatibilityView(string ProtonScript, string SteamRoot, string DataDirectory,
    bool Registered, string RegisteredRoot, bool CanRegister, bool UseSteamPrefix, string SteamPrefix,
    RuntimeEntry[] Runtimes, PrefixEntry[] Prefixes);

// One settings service for all regions. Only the optional existing Steam prefix belongs to Global.
// The executable comes from the trusted process entry, never from a renderer request.
internal sealed class FrontendCompatibility(string root, string? launcherEntry = null,
    Func<IEnumerable<string>>? steamRoots = null, Func<LinuxSettings, CancellationToken, Task<string>>? diagnose = null, Action? ensureGameIdle = null)
{
    private IEnumerable<string> Roots(LinuxSettings settings) => (steamRoots?.Invoke() ?? ProtonDiscovery.GetSteamRoots())
        .Append(settings.SteamRoot).Where(Path.IsPathFullyQualified).Distinct(StringComparer.Ordinal);

    internal CompatibilityView Read()
    {
        var settings = LinuxSettings.Load(root);
        var discovered = ProtonDiscovery.Discover(Roots(settings), steamRoots == null
            ? ["/usr/share/steam/compatibilitytools.d", "/usr/local/share/steam/compatibilitytools.d"] : []).ToList();
        if (settings.ProtonScript.Length > 0 && File.Exists(settings.ProtonScript) && discovered.All(x => x.Script != settings.ProtonScript))
            discovered.Add(ProtonDiscovery.FromScript(settings.ProtonScript, settings.SteamRoot));
        var registeredRoot = RegisteredRoot(settings);
        var global = settings.RegionProfiles.TryGetValue("ffxiv",out var profile) ? profile : new RegionSettings();
        return new(settings.ProtonScript, settings.SteamRoot, settings.CompatibilityData, registeredRoot.Length > 0,
            registeredRoot, launcherEntry != null && File.Exists(launcherEntry), global.UseSteamPrefix, global.SteamCompatibilityData,
            discovered.Select(x => new RuntimeEntry(x.Name, x.Script, x.IsReady, x.RequiredRuntimeAppId, x.SteamRoot)).ToArray(),
            SteamPrefixDiscovery.Discover(Roots(settings)).Select(x => new PrefixEntry(x.ToString(), x.DataDirectory)).ToArray());
    }

    internal static string RegisteredRoot(LinuxSettings settings) => new[] { settings.RegisteredSteamRoot, settings.SteamRoot }
        .FirstOrDefault(x => x.Length > 0 && SteamToolRegistration.IsRegistered(x)) ?? "";

    // Acknowledgement of persisted settings, deliberately without a discovery scan.
    private CompatibilityView Committed(LinuxSettings settings)
    {
        var global = settings.RegionProfiles.TryGetValue("ffxiv", out var profile) ? profile : new RegionSettings();
        return new(settings.ProtonScript, settings.SteamRoot, settings.CompatibilityData,
            settings.RegisteredSteamRoot.Length > 0, settings.RegisteredSteamRoot, launcherEntry != null,
            global.UseSteamPrefix, global.SteamCompatibilityData, [], []);
    }

    internal CompatibilityView Update(CompatibilityChange change)
    {
        if (change == new CompatibilityChange()) throw new ArgumentException("Empty compatibility update.");
        ValidatePath(change.ProtonScript); ValidatePath(change.SteamRoot, true);
        ValidatePath(change.DataDirectory); ValidatePath(change.SteamPrefix, true);
        // Prefix settings have their own persistence boundary; don't partially commit unrelated edits.
        var prefixEdit = change.UseSteamPrefix != null || change.SteamPrefix != null;
        if (prefixEdit && (change.ProtonScript != null || change.SteamRoot != null || change.DataDirectory != null || change.Registered != null))
            throw new ArgumentException("Mixed compatibility update.");
        (ensureGameIdle ?? GameProcessGuard.EnsureGameSettingsIdle)();
        var settings = LinuxSettings.Load(root);
        if (prefixEdit)
        {
            var global = settings.RegionProfiles["ffxiv"];
            var enabled = change.UseSteamPrefix ?? global.UseSteamPrefix;
            var path = change.SteamPrefix ?? global.SteamCompatibilityData;
            // Disabling must work even when the previously selected environment has disappeared.
            if (enabled) path = SteamPrefixDiscovery.Normalize(path);
            global.UseSteamPrefix = enabled; global.SteamCompatibilityData = path;
            settings.SaveRegionPreferences("ffxiv");
            return Committed(settings);
        }
        var steam = change.SteamRoot ?? settings.SteamRoot;
        var proton = change.ProtonScript == null ? null : ProtonDiscovery.FromScript(change.ProtonScript, steam);
        var oldRoot = RegisteredRoot(settings);
        var enabledRegistration = change.Registered ?? oldRoot.Length > 0;
        var registrationEdit = change.Registered != null || change.SteamRoot != null && oldRoot.Length > 0;
        if (registrationEdit && enabledRegistration && (launcherEntry == null || !File.Exists(launcherEntry)))
            throw new InvalidOperationException("The frontend launcher entry is unavailable.");
        try
        {
            if (registrationEdit)
            {
                if (enabledRegistration)
                {
                    var alreadyRegistered = SteamToolRegistration.IsRegistered(steam);
                    SteamToolRegistration.Install(steam, launcherEntry!);
                    try { if (oldRoot.Length > 0 && oldRoot != steam) SteamToolRegistration.Remove(oldRoot); }
                    catch
                    {
                        if (!alreadyRegistered)
                            try { SteamToolRegistration.Remove(steam); }
                            catch (Exception ex) { new SessionDiagnostics(root).Write(FrontendErrors.Diagnostic(ex), "shared", "ERROR"); }
                        throw;
                    }
                }
                else if (oldRoot.Length > 0) SteamToolRegistration.Remove(oldRoot);
            }
            settings.SteamRoot = steam;
            if (proton != null) settings.ProtonScript = proton.Script;
            if (change.DataDirectory != null) settings.CompatibilityData = change.DataDirectory;
            settings.RegisteredSteamRoot = registrationEdit ? enabledRegistration ? steam : "" : oldRoot;
            settings.SaveGlobalPreferences();
        }
        catch
        {
            // Keep the recorded checkbox tied to real files after partial registration/removal failure.
            var stored = LinuxSettings.Load(root);
            stored.RegisteredSteamRoot = new[] { steam, oldRoot }.FirstOrDefault(x => x.Length > 0 && SteamToolRegistration.IsRegistered(x)) ?? "";
            try { stored.SaveGlobalPreferences(); }
            catch (Exception ex) { new SessionDiagnostics(root).Write(FrontendErrors.Diagnostic(ex), "shared", "ERROR"); }
            throw;
        }
        return Committed(settings);
    }

    internal object Diagnose(FrontendOperations operations)
    {
        operations.EnsureIdle();
        var settings = LinuxSettings.Load(root);
        return operations.Start(settings.SelectedRegion, async operation =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            string result;
            if (diagnose != null) result = await diagnose(settings, timeout.Token).WaitAsync(timeout.Token);
            else
            {
                if (settings.ProtonScript.Length == 0) throw new CredentialValidationException("No Proton selected.", "请先选择 Proton。");
                var proton = ProtonDiscovery.FromScript(settings.ProtonScript, settings.SteamRoot);
                if (settings.SelectedRegion == "ffxiv" && settings.Current.UseSteamPrefix) SteamPrefixDiscovery.EnsureIdle(settings.Current.SteamCompatibilityData);
                var runner = new CompatibilityRunner(settings.GetCompatibility(proton));
                runner.Validate(); result = await runner.DiagnoseAsync(timeout.Token).WaitAsync(timeout.Token);
            }
            new SessionDiagnostics(root).Write(result, settings.SelectedRegion, "INFO");
            operation.Status("Compatibility check passed. Game and plugin compatibility still require an in-game test.");
            operation.SetResult(new { message = "环境检测通过。此检测不代表游戏或插件已通过实机验证。" });
        });
    }

    private static void ValidatePath(string? value, bool allowEmpty = false)
    {
        if (value != null && (value.Length > 4096 || value.Contains('\0') || !(allowEmpty && value.Length == 0) && !Path.IsPathFullyQualified(value)))
            throw new ArgumentException("An absolute path is required.");
    }
}
