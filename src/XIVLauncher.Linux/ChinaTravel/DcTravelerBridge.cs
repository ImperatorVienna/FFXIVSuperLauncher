using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Serilog;
using XIVLauncher.DCTravel;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Workflow;

namespace XIVLauncher.Linux.ChinaTravel;

// DCTravelerX's existing protocol has no authentication header. Bind only to loopback,
// reject browser requests, and expose an explicit method list rather than reflection.
public sealed class DcTravelerBridge : IAsyncDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly DCTravelClient client;
    private int disposed;
    private readonly Func<string, JsonElement[], Task<object?>> dispatch;
    private readonly Task serving;
    private readonly Action<string, string>? diagnostic;
    internal static readonly JsonSerializerOptions WireOptions = CreateWireOptions();
    public int Port { get; }

    public DcTravelerBridge(LoginSessionRefreshContext refresh, LoginArea[] areas, Action<string> setArea, Action<string, string>? diagnostic = null)
        : this(null, refresh, areas, setArea, diagnostic: diagnostic) { }

    internal DcTravelerBridge(Func<string, JsonElement[], Task<object?>>? handler,
        LoginSessionRefreshContext? refresh = null, LoginArea[]? areas = null, Action<string>? setArea = null, HttpMessageHandler? transport = null, Action<string, string>? diagnostic = null)
    {
        this.diagnostic = diagnostic;
        client = new DCTravelClient("", transport);
        if (refresh != null) client.BindLoginSessionRefresh(refresh);
        dispatch = handler ?? DispatchAsync;
        // HttpListener cannot report an OS-assigned port. Retry the small reservation race.
        for (var attempt = 0; ; attempt++)
        {
            using var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            Port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/dctravel/");
            try { listener.Start(); break; }
            catch (HttpListenerException) when (attempt < 9) { }
        }
        serving = ServeAsync();
        Log.Information("[DCTravelerX] Local bridge listening on port {Port}", Port);

        async Task<object?> DispatchAsync(string method, JsonElement[] args)
        {
            var count = ParameterCount(method);
            if (args.Length != count) throw new ArgumentException("Invalid parameter count.");
            T Arg<T>(int index) => args[index].Deserialize<T>(WireOptions) ?? throw new ArgumentException("Null parameter.");
            if (method == "SetSdoArea")
            {
                var name = Arg<string>(0);
                if (areas?.Any(x => x.AreaName == name) != true) throw new ArgumentException("Unknown data center.");
                setArea!(name);
                return "";
            }
            if (method == "RefreshGameSessionId") return await refresh!.RefreshGameSessionIdAsync();
            await client.EnsureInitializedAsync();
            switch (method)
            {
                case "QueryGroupListTravelSource": return await client.QueryGroupListTravelSource();
                case "QueryGroupListTravelTarget": return await client.QueryGroupListTravelTarget(Arg<int>(0), Arg<int>(1));
                case "QueryRoleList": return await client.QueryRoleList(Arg<int>(0), Arg<int>(1));
                case "QueryMigrationOrders":
                    if (!await client.InitOrderPage()) throw new IOException("Travel history unavailable.");
                    return await client.QueryMigrationOrders(Arg<int>(0));
                case "QueryOrderStatus": return await client.QueryOrderStatus(Arg<string>(0));
                case "TravelOrder": return await client.TravelOrder(Arg<DCTravelGroup>(0), Arg<DCTravelGroup>(1), Arg<DCTravelCharacter>(2));
                case "TravelBack": return await client.TravelBack(Arg<string>(0), Arg<int>(1), Arg<string>(2), Arg<string>(3));
                case "MigrationConfirmOrder": await client.MigrationConfirmOrder(Arg<string>(0), Arg<bool>(1)); return "";
                default: throw new ArgumentException("Unsupported method.");
            }
        }
    }

    private static int ParameterCount(string method) => method switch
    {
        "QueryGroupListTravelSource" or "RefreshGameSessionId" => 0,
        "QueryMigrationOrders" or "QueryOrderStatus" or "SetSdoArea" => 1,
        "QueryGroupListTravelTarget" or "QueryRoleList" or "MigrationConfirmOrder" => 2,
        "TravelOrder" => 3, "TravelBack" => 4,
        _ => throw new ArgumentException("Unsupported DCTravelerX method.")
    };

    internal static string SerializeLobbyHosts(LoginArea[] areas) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(areas.Select(a => new { Areaid = a.AreaID, AreaStat = a.AreaStatus, a.AreaOrder,
            a.AreaName, Areatype = a.AreaType, a.AreaLobby, AreaGm = a.AreaGM, a.AreaPatch, a.AreaConfigUpload }))));

    private static JsonSerializerOptions CreateWireOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            foreach (var property in info.Properties)
                if (property.Name == "AreaID") property.Name = "AreaId";
                else if (property.Name == "GroupID") property.Name = "GroupId";
        });
        return new() { PropertyNameCaseInsensitive = true, TypeInfoResolver = resolver };
    }

    private async Task ServeAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().WaitAsync(lifetime.Token); }
            catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException) { break; }
            try
            {
                var request = context.Request;
                if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/dctravel/" ||
                    request.Headers["Origin"] != null || request.Headers["Sec-Fetch-Site"] != null ||
                    request.ContentType?.Split(';')[0].Trim() != "application/json")
                { context.Response.StatusCode = 403; continue; }
                object? result = null;
                string? error = null;
                var diagnosticMethod = "InvalidRequest";
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(10));
                    using var body = new MemoryStream();
                    var buffer = new byte[4096];
                    int read;
                    while ((read = await request.InputStream.ReadAsync(buffer, deadline.Token)) > 0)
                    {
                        if (body.Length + read > 65536) throw new ArgumentException("Request too large.");
                        body.Write(buffer, 0, read);
                    }
                    using var json = JsonDocument.Parse(body.ToArray());
                    var method = json.RootElement.GetProperty("Method").GetString() ?? throw new ArgumentException();
                    var args = json.RootElement.GetProperty("Params").EnumerateArray().ToArray();
                    _ = ParameterCount(method); // Only known method names reach diagnostics.
                    diagnosticMethod = method;
                    result = await dispatch(method, args).WaitAsync(lifetime.Token);
                    if (result is DCTravelOrderInfo order)
                        Record($"DC travel QueryOrderStatus completed: status={(int)order.Status}.", "INFO");
                    else if (method is "TravelOrder" or "TravelBack" or "MigrationConfirmOrder")
                        Record($"DC travel {method} completed. No automatic resubmission.", "INFO");
                }
                catch (Exception ex) when (!lifetime.IsCancellationRequested)
                {
                    // Never return tickets, cookies, request bodies, or raw upstream exceptions.
                    Log.Warning("[DCTravelerX] RPC failed: {Type}, service code {Code}", ex.GetType().Name,
                        (ex as DCTravelAPIException)?.ErrorCode);
                    Record(FailureDiagnostic(diagnosticMethod, ex), "ERROR");
                    error = FormatError(ex);
                }
                context.Response.ContentType = "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(context.Response.OutputStream, new { Result = result, Error = error }, WireOptions, lifetime.Token);
            }
            catch (Exception ex) when (ex is IOException or HttpListenerException or OperationCanceledException or ObjectDisposedException) { }
            finally { context.Response.Close(); }
        }
    }

    private void Record(string message, string level)
    {
        // Diagnostics must never turn a successful remote mutation into a failed RPC.
        try { diagnostic?.Invoke(message, level); } catch { }
    }

    internal static string FailureDiagnostic(string method, Exception error) =>
        $"DC travel {method} failed: {error.GetType().Name}; service code={(error as DCTravelAPIException)?.ErrorCode}; " +
        $"HTTP status={(error as HttpRequestException)?.StatusCode}.\n{error.StackTrace}";

    internal static string FormatError(Exception ex) => ex switch
    {
        GameSessionLoginRequiredException => "跨区登录授权已失效，请打开启动器扫码恢复；无需退出游戏。",
        ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException => "Invalid DCTravelerX request.",
        DCTravelAPIException { IsServiceMaintenance: true } => "官方超域旅行服务维护中，请稍后重试。",
        DCTravelAPIException api when api.ErrorCode == -10600032 || api.Message.Contains("用户数量较多") || api.Message.Contains("稍晚再次尝试")
            => "目标服务器繁忙，请您稍晚再次尝试。",
        // Do not call an ambiguous submission '传送失败': DCTravelerX automatically retries that phrase.
        _ => "跨区服务请求未能确认结果，请先查询官方订单状态。"
    };

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        listener.Close();
        try { client.EndSession(); await serving; }
        finally { client.Dispose(); lifetime.Dispose(); }
        Log.Information("[DCTravelerX] Local bridge closed on port {Port}", Port);
    }
}
