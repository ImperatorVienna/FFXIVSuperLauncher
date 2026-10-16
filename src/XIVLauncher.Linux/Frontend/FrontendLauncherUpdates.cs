using XIVLauncher.Linux.Updates;

namespace XIVLauncher.Linux.Frontend;

internal sealed record UpdateCheckRequest(bool Refresh = false);
internal sealed record UpdateInstallRequest(string Release);
internal sealed record LauncherUpdateView(string State, string? Release, string? Version, bool Available, bool CanInstall, string? Error);

// Version checks run independently of authentication and game operations. Installation shares the busy guard.
internal sealed class FrontendLauncherUpdates(string root, FrontendApplication application, LauncherUpdates? service = null, IUpdateInstaller? installer = null) : IAsyncDisposable
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(15) }) { Timeout = TimeSpan.FromMinutes(15) };
    private readonly IUpdateInstaller? installation = installer ?? Platforms.PlatformServices.UpdateInstaller;
    private LauncherUpdates? updateService = service;
    private LauncherUpdates Updates => updateService ??= new LauncherUpdates(Http, UpdateDistribution.Load());
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private Task? task;
    private LauncherRelease? release;
    private string? releaseId;
    private string state = "idle";
    private string? error;
    private static Version Current => new(typeof(FrontendHost).Assembly.GetName().Version!.ToString(3));
    internal LauncherUpdateView Read(UpdateCheckRequest request)
    {
        lock (gate)
        {
            var settings = LinuxSettings.Load(root);
            if (task is not { IsCompleted: false } && (request.Refresh || state == "idle" && settings.SetupComplete && settings.AutoCheckUpdates))
            {
                state = "checking"; error = null; release = null; releaseId = null;
                task = Task.Run(Check);
            }
            return new(state, releaseId, release?.Version.ToString(), release != null && release.Version > Current,
                installation?.CanInstall(root) == true && release?.Manifest != null && release.Signature != null, error);
        }
    }
    private async Task Check()
    {
        try
        {
            application.EnsureIntegration(); // Default settings, stable entry and desktop entry precede updates.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var found = await Updates.CheckAsync(timeout.Token).WaitAsync(timeout.Token);
            lock (gate) { release = found; releaseId = found == null ? null : Guid.NewGuid().ToString("N"); state = "ready"; }
        }
        catch (Exception ex)
        {
            new SessionDiagnostics(root).Write(FrontendErrors.Diagnostic(ex), "shared", "WARN");
            lock (gate) { state = "failed"; error = "检查更新失败，请稍后重试。"; }
        }
    }
    private LauncherRelease Find(string id)
    {
        lock (gate) return state == "ready" && releaseId == id && release != null ? release : throw new ArgumentException("The release selection has changed.");
    }
    internal object Open(UpdateInstallRequest request)
    { OfficialBrowser.Open(Find(request.Release).Page, root); return new { opened = true }; }
    internal object Install(UpdateInstallRequest request, FrontendOperations operations)
    {
        operations.EnsureIdle(); var selected = Find(request.Release);
        if (installation?.CanInstall(root) != true || selected.Version <= Current) throw new ArgumentException("No installable update selected.");
        return operations.Start(LinuxSettings.Load(root).SelectedRegion, async operation =>
        {
            using var cacheLease = DownloadCacheLock.Acquire(root);
            GameProcessGuard.EnsureGamesStopped();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, lifetime.Token); timeout.CancelAfter(TimeSpan.FromMinutes(15));
            var token = timeout.Token;
            operation.Status("Verifying launcher update.");
            var update = await Updates.VerifyAsync(selected, token);
            var image = installation.LauncherPath;
            operation.Status("Downloading launcher update.");
            var stage = await Updates.StageAsync(update, image,
                percent => operation.Download(new("launcher", Path.GetFileName(image), update.Size * percent / 100, update.Size, true)), token);
            try
            {
                token.ThrowIfCancellationRequested(); GameProcessGuard.EnsureGamesStopped();
                using var helper = installation.Start(stage, update.Sha256, new SessionDiagnostics(root).FilePath, update.Version);
                // The platform helper waits for shutdown before replacing/restarting the launcher.
                operation.Status("Update verified. Restarting the launcher to finish installation.");
                operation.CommitResult(new { exitForUpdate = true });
            }
            catch { File.Delete(stage); throw; }
        });
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel(); if (task != null) try { await task; } catch (OperationCanceledException) { }
        lifetime.Dispose();
    }
}
