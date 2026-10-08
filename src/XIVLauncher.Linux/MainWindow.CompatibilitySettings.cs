using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private bool compatibilityAutoSaveReady;
    private bool applyingCompatibility;
    private void EnableCompatibilityAutoSave()
    {
        if (compatibilityAutoSaveReady) return;
        compatibilityAutoSaveReady = true;
        registerSteam.IsCheckedChanged += (_, _) => ApplyCompatibilitySettings();
        steamPath.LostFocus += (_, _) => ApplyCompatibilitySettings();
        dataPath.LostFocus += (_, _) => ApplyCompatibilitySettings();
    }
    private void ApplyCompatibilitySettings()
    {
        if (!compatibilityAutoSaveReady || !settingsLoaded || applyingCompatibility || operation != null) return;
        applyingCompatibility = true;
        var oldRoot = settings.RegisteredSteamRoot;
        var oldSteam = settings.SteamRoot; var oldData = settings.CompatibilityData; var oldProton = settings.ProtonScript;
        try
        {
            var root = steamPath.Text?.Trim() ?? "";
            var data = dataPath.Text?.Trim() ?? "";
            if (!Path.IsPathRooted(data)) throw new IOException("The Proton data directory must be an absolute path.");
            var enabled = registerSteam.IsChecked == true;
            var registered = SteamToolRegistration.IsRegistered(root);
            if (settings.SteamRoot == root && settings.CompatibilityData == data
                && settings.ProtonScript == (proton.SelectedItem as ProtonInstallation)?.Script
                && enabled == registered && (!enabled ? string.IsNullOrEmpty(oldRoot) : oldRoot == root)) return;
            if (enabled)
            {
                SteamToolRegistration.Install(root, Updates.AppImageEntry.RegistrationPath);
                if (oldRoot.Length != 0 && oldRoot != root) SteamToolRegistration.Remove(oldRoot);
            }
            else if (oldRoot.Length != 0) SteamToolRegistration.Remove(oldRoot);
            settings.SteamRoot = root; settings.CompatibilityData = data;
            settings.ProtonScript = (proton.SelectedItem as ProtonInstallation)?.Script ?? oldProton;
            settings.RegisteredSteamRoot = enabled ? root : "";
            settings.Save();
            status.Text = "Compatibility tool settings applied.";
        }
        catch (Exception ex)
        {
            // Reflect actual registration after a partial filesystem failure, not the requested checkbox.
            var root = steamPath.Text?.Trim() ?? "";
            var actual = SteamToolRegistration.IsRegistered(root) ? root
                : SteamToolRegistration.IsRegistered(oldRoot) ? oldRoot : "";
            registerSteam.IsChecked = actual.Length != 0;
            settings.RegisteredSteamRoot = actual;
            settings.SteamRoot = oldSteam; settings.CompatibilityData = oldData; settings.ProtonScript = oldProton;
            steamPath.Text = oldSteam; dataPath.Text = oldData;
            proton.SelectedItem = (proton.ItemsSource as IEnumerable<ProtonInstallation>)?.FirstOrDefault(x => x.Script == oldProton);
            ReportFailure(ex);
        }
        finally { applyingCompatibility = false; }
    }
}
