namespace XIVLauncher.Linux.Frontend;

internal sealed record OfficialRead(bool Refresh = false);
internal sealed record OfficialOpen(string Key, string Item);
internal sealed record OfficialEntry(string Id, string Title, string? Date);
internal sealed record OfficialView(string Key, string Region, string State, string[] Links,
    OfficialEntry[] Activities, OfficialEntry[] Notices, string? Error = null);

// Public content has its own lifetime and deadline; it never occupies a game-operation slot.
internal sealed class FrontendOfficial : IAsyncDisposable
{
    private readonly string root;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly Func<string, string, string, CancellationToken, Task<OfficialNews>> fetch;
    private readonly Action<string> open;
    private readonly TimeSpan deadline;
    private readonly object gate = new();
    private readonly Dictionary<string, string> destinations = new();
    private CancellationTokenSource? loading;
    private Task pending = Task.CompletedTask;
    private OfficialView? view;
    private bool disposed;

    internal FrontendOfficial(string root,
        Func<string, string, string, CancellationToken, Task<OfficialNews>>? fetch = null,
        Action<string>? open = null, TimeSpan? deadline = null)
    {
        this.root = root;
        this.fetch = fetch ?? ((region, version, language, token) => OfficialContent.LoadAsync(client, region, token, version, language));
        this.open = open ?? (url => OfficialBrowser.Open(url, root, Environment.GetEnvironmentVariable("FFXIV_ELECTRON")));
        this.deadline = deadline ?? TimeSpan.FromSeconds(8);
    }

    private (string Key, string Region, string Version, string Language) Context()
    {
        var settings = LinuxSettings.Load(root);
        var profile = settings.SelectedRegion=="ffxiv" ? settings.RegionProfiles["ffxiv"] : new RegionSettings();
        var key = settings.SelectedRegion == "ffxiv"
            ? $"ffxiv:{InternationalWeb.Preferences(profile.CdKeyVersion, profile.EuShopLanguage).NewsLanguage}"
            : settings.SelectedRegion;
        return (key, settings.SelectedRegion, profile.CdKeyVersion, profile.EuShopLanguage);
    }

    internal OfficialView Read(OfficialRead request)
    {
        var context = Context();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (view?.Key == context.Key && (!request.Refresh || view.State == "loading")) return view;
            loading?.Cancel();
            loading?.Dispose();
            loading = new CancellationTokenSource(deadline);
            var source = loading;
            var token = source.Token;
            destinations.Clear();
            var links = OfficialContent.LinksFor(context.Region, context.Version, context.Language);
            void Add(string id, string? url) { if (url != null) destinations.Add(id, url); }
            Add("home", links.Home); Add("status", links.ServerStatus); Add("shop", links.Shop);
            Add("subscription", links.Subscription); Add("mog", links.MogStation);
            view = new(context.Key, context.Region, "loading", destinations.Keys.ToArray(), [], []);
            pending = Task.Run(async () =>
            {
                try
                {
                    // Bound our wait even if a future provider fails to honour cancellation.
                    var news = await fetch(context.Region, context.Version, context.Language, token).WaitAsync(token);
                    lock (gate)
                    {
                        if (disposed || loading != source || token.IsCancellationRequested) return;
                        OfficialEntry[] Entries(IReadOnlyList<OfficialArticle> articles) => articles.Take(6)
                            .Where(x => ValidUrl(x.Url)).Select(x =>
                            {
                                var id = Guid.NewGuid().ToString("N"); destinations.Add(id, x.Url);
                                return new OfficialEntry(id, x.Title, x.Date);
                            }).ToArray();
                        view = view! with { State = "ready", Activities = Entries(news.Activities), Notices = Entries(news.Notices) };
                    }
                }
                catch (Exception ex)
                {
                    lock (gate)
                    {
                        if (disposed || loading != source) return;
                        view = view! with { State = "failed", Error = ex is OperationCanceledException ? "timeout" : "unavailable" };
                    }
                    try { new SessionDiagnostics(root).Write("Official content: " + FrontendErrors.Diagnostic(ex), context.Region, "ERROR"); }
                    catch (IOException) { } // A log-file failure must not become an unobserved background failure.
                    catch (UnauthorizedAccessException) { }
                }
            });
            return view;
        }
    }

    internal object Open(OfficialOpen request)
    {
        var context = Context();
        string url;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (request.Key != context.Key || view?.Key != context.Key ||
                !destinations.TryGetValue(request.Item, out url!) || !ValidUrl(url))
                throw new ArgumentException("Unknown or stale official entry.");
        }
        // Only backend-resolved destinations reach the separate, sandboxed browser.
        open(url);
        return new { opened = true };
    }

    private static bool ValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);

    public async ValueTask DisposeAsync()
    {
        Task task;
        lock (gate) { disposed = true; if (!pending.IsCompleted) loading?.Cancel(); task = pending; }
        await task;
        loading?.Dispose();
        client.Dispose();
    }
}
