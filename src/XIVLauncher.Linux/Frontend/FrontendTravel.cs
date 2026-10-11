using XIVLauncher.DCTravel;
using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.Login.Models;

namespace XIVLauncher.Linux.Frontend;

internal sealed record TravelRequest(string Kind, string? Account = null, string? Session = null, string? Revision = null,
    string? Source = null, string? Character = null, string? Target = null, string? Order = null, string? Current = null, int? Page = null);
internal sealed record TravelChoice(string Id, string Name);
internal sealed record TravelAreaView(string Id, string Name, TravelChoice[] Groups);
internal sealed record TravelOrderView(string Id, string Name, string From, string To, string Created);
internal sealed record TravelView(string Session, string Revision, string Account, string Source, string Character,
    TravelAreaView[] Sources, TravelChoice[] Characters, TravelAreaView[] Targets, TravelOrderView[] Orders,
    int Page, int Pages, string? CompletedArea);

// Travel cookies, role IDs and order IDs stay here. The renderer sends only choices from a returned snapshot.
internal sealed class FrontendTravel(string root, Func<ChinaTravelSession>? createSession = null,
    Func<LinuxSettings, ChinaTravelSession, FrontendOperation, Task>? authenticate = null, Action? ensureIdle = null) : IDisposable
{
    private ChinaTravelSession? session;
    private string sessionId = "", revision = "", selectedSource = "", selectedCharacter = "";
    private List<DCTravelArea> sources = [], targets = [];
    private readonly Dictionary<string, DCTravelCharacter> characters = new();
    private readonly Dictionary<string, DCTravelMigrationOrder> orders = new();
    private int page = 1, pages = 1;
    private static string NewId() => Guid.NewGuid().ToString("N");
    private static string AreaId(DCTravelArea area) => area.AreaID.ToString();
    private static string GroupId(DCTravelGroup group) => $"{group.AreaID}:{group.GroupID}";

    internal object Start(TravelRequest request, FrontendOperations operations)
    {
        operations.EnsureIdle(); Validate(request);
        var settings = LinuxSettings.Load(root);
        if (settings.SelectedRegion != "ffxiv_cn") throw new ArgumentException("Travel is only available for China.");
        if (request.Kind == "open") FrontendAccounts.Validate("ffxiv_cn", request.Account!);
        else if (session == null || request.Session != sessionId || settings.Account != session.Account)
            throw new ArgumentException("The travel session has changed.");
        if (request.Kind is "transfer" or "return")
        {
            if (request.Revision != revision) throw new ArgumentException("Stale travel confirmation.");
            // Consume before any server request; failure or cancellation must never replay this submission.
        }
        revision = NewId();
        return operations.Start("ffxiv_cn", operation => RunAsync(request, operation));
    }

    private static void Validate(TravelRequest request)
    {
        var allowed = request.Kind switch
        {
            "open" => new[] { "account" }, "source" => ["session", "source"], "character" => ["session", "character"],
            "history" => ["session", "page"], "transfer" => ["session", "revision", "target"],
            "return" => ["session", "revision", "order", "current"], _ => throw new ArgumentException("Unknown travel action.")
        };
        var values = new Dictionary<string, object?> { ["account"] = request.Account, ["session"] = request.Session,
            ["revision"] = request.Revision, ["source"] = request.Source, ["character"] = request.Character,
            ["target"] = request.Target, ["order"] = request.Order, ["current"] = request.Current, ["page"] = request.Page };
        if (values.Any(x => allowed.Contains(x.Key) ? x.Value == null : x.Value != null) ||
            values.Values.OfType<string>().Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 256) || request.Page is < 1 or > 10000)
            throw new ArgumentException("Invalid travel request.");
    }

    private async Task RunAsync(TravelRequest request, FrontendOperation operation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
        timeout.CancelAfter(request.Kind is "transfer" or "return" ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(3));
        var token = timeout.Token;
        string? completedArea = null;
        if (request.Kind == "open")
        {
            Dispose();
            (ensureIdle ?? GameProcessGuard.EnsureGamesStopped)();
            var settings = LinuxSettings.Load(root);
            AccountProfiles.Select(settings.Current, request.Account!); settings.Current.RememberPassword = true; settings.Save();
            session = createSession?.Invoke() ?? new ChinaTravelSession();
            sessionId = NewId(); revision = NewId();
            try
            {
                if (authenticate != null) await authenticate(settings, session, operation).WaitAsync(token);
                else
                {
                    var result = await ChinaLoginSession.AuthenticateAsync(settings,
                        FrontendLoginRequest.Create(settings, operation, session: session), operation.Status, token);
                    if (result.State != LoginState.Ok || !session.Authenticated) throw new IOException("Travel sign-in failed.");
                }
                session.Account = settings.Account;
                operation.ClearNotification();
            }
            catch { Dispose(); throw; }
        }
        var client = session!.Client;
        client.BeginSession();
        using var cancel = token.Register(client.EndSession);
        await client.GetValidCookie().WaitAsync(token);
        switch (request.Kind)
        {
            case "open":
                sources = await client.QueryGroupListTravelSource().WaitAsync(token);
                await SelectSource(sources.FirstOrDefault(), token);
                await History(1, token);
                break;
            case "source":
                await SelectSource(sources.SingleOrDefault(x => AreaId(x) == request.Source) ?? throw new ArgumentException("Unknown source."), token);
                break;
            case "character":
                if (!characters.ContainsKey(request.Character!)) throw new ArgumentException("Unknown character.");
                await SelectCharacter(request.Character!, token);
                break;
            case "history":
                if (request.Page > pages) throw new ArgumentException("Invalid history page.");
                await History(request.Page!.Value, token);
                break;
            case "transfer":
                if (!characters.TryGetValue(selectedCharacter, out var character)) throw new ArgumentException("Select a character.");
                var source = sources.SelectMany(x => x.GroupList).Single(x => x.AreaID == character.AreaID && x.GroupID == character.GroupID);
                var target = targets.SelectMany(x => x.GroupList).SingleOrDefault(x => GroupId(x) == request.Target) ?? throw new ArgumentException("Unknown destination.");
                (ensureIdle ?? GameProcessGuard.EnsureGamesStopped)();
                await Complete(await client.TravelOrder(target, source, character).WaitAsync(token), token);
                completedArea = target.AreaName;
                break;
            case "return":
                if (!orders.TryGetValue(request.Order!, out var order)) throw new ArgumentException("Unknown travel record.");
                var current = sources.SelectMany(x => x.GroupList).SingleOrDefault(x => GroupId(x) == request.Current) ?? throw new ArgumentException("Select the current world.");
                (ensureIdle ?? GameProcessGuard.EnsureGamesStopped)();
                await Complete(await client.TravelBack(order.OrderID, current.GroupID, current.GroupCode, current.GroupName).WaitAsync(token), token);
                completedArea = order.SourceAreaName;
                break;
        }
        if (completedArea != null)
        {
            // Re-read settings so unrelated preferences are not overwritten by a long server operation.
            var settings = LinuxSettings.Load(root); settings.RegionProfiles["ffxiv_cn"].AreaName = completedArea; settings.SaveRegionPreferences("ffxiv_cn");
            characters.Clear(); targets.Clear(); selectedCharacter = "";
            // A history refresh failure must not turn a confirmed transfer into a reported transfer failure.
            orders.Clear(); page = pages = 1;
            operation.Status("超域操作已完成。");
        }
        revision = NewId();
        operation.SetResult(View(completedArea));

        async Task Complete(string id, CancellationToken ct)
        {
            await TravelOrderMonitor.WaitAsync(client.QueryOrderStatus, client.MigrationConfirmOrder, id, operation.Status, ct);
        }
    }

    private async Task SelectSource(DCTravelArea? source, CancellationToken token)
    {
        characters.Clear(); targets.Clear(); selectedCharacter = ""; selectedSource = source == null ? "" : AreaId(source);
        if (source == null) return;
        foreach (var group in source.GroupList)
        {
            var roles = await session!.Client.QueryRoleList(source.AreaID, group.GroupID).WaitAsync(token);
            foreach (var role in roles) { role.ServerName = group.GroupName; characters.Add(NewId(), role); }
            if (characters.Count > 1000) throw new IOException("Too many characters.");
        }
        if (characters.Count > 0) await SelectCharacter(characters.Keys.First(), token);
    }
    private async Task SelectCharacter(string id, CancellationToken token)
    {
        selectedCharacter = id; targets.Clear();
        var role = characters[id];
        targets = await session!.Client.QueryGroupListTravelTarget(role.AreaID, role.GroupID).WaitAsync(token);
    }
    private async Task History(int requestedPage, CancellationToken token)
    {
        var client = session!.Client;
        if (!await client.InitOrderPage().WaitAsync(token)) throw new IOException("Travel history is unavailable.");
        var data = await client.QueryMigrationOrders(requestedPage).WaitAsync(token);
        orders.Clear(); foreach (var order in data.Orders.Take(100)) orders.Add(NewId(), order);
        page = requestedPage; pages = Math.Clamp(data.TotalPageNum, 1, 10000);
    }
    private TravelView View(string? completedArea) => new(sessionId, revision, session!.Account, selectedSource, selectedCharacter,
        Areas(sources), characters.Select(x => new TravelChoice(x.Key, x.Value.Name + " — " + x.Value.ServerName)).ToArray(), Areas(targets),
        orders.Select(x => new TravelOrderView(x.Key, x.Value.RoleName, x.Value.SourceAreaName + " / " + x.Value.SourceGroupName,
            x.Value.TargetAreaName + " / " + x.Value.TargetGroupName, x.Value.CreateTime)).ToArray(), page, pages, completedArea);
    private static TravelAreaView[] Areas(IEnumerable<DCTravelArea> areas) => areas.Select(a => new TravelAreaView(AreaId(a), a.AreaName,
        a.GroupList.Select(g => new TravelChoice(GroupId(g), g.GroupName)).ToArray())).ToArray();
    public void Dispose()
    {
        session?.Dispose(); session = null; sessionId = revision = selectedSource = selectedCharacter = "";
        sources.Clear(); targets.Clear(); characters.Clear(); orders.Clear(); page = pages = 1;
    }
}
