using System.Collections.Specialized;
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace XIVLauncher.Linux.Platforms.Windows;

// One socket per game session, with separately owned serial queues for travel/recovery.
// No HTTP.sys registration, URL ACL, console logger, configuration files or system service.
internal sealed class WindowsLocalHttpListener : LocalHttpListener
{
    private static readonly object Gate = new();
    private static readonly Dictionary<int, Host> Hosts = new();
    private readonly Channel<Context> requests = Channel.CreateBounded<Context>(32);
    private readonly CancellationTokenSource closed = new();
    private readonly Host host;
    private readonly string prefix;
    private bool disposed;
    public override int Port => host.Port;
    private WindowsLocalHttpListener(Host host, string prefix) { this.host = host; this.prefix = prefix; }
    public new static WindowsLocalHttpListener Listen(int port, string prefix)
    {
        lock (Gate)
        {
            var created = !Hosts.TryGetValue(port, out var host);
            host ??= new Host(port);
            if (host.Routes.ContainsKey(prefix)) throw new IOException("Local endpoint is already registered.");
            var listener = new WindowsLocalHttpListener(host, prefix);
            host.Routes.Add(prefix, listener);
            if (created) Hosts.Add(host.Port, host);
            return listener;
        }
    }
    public override async Task<LocalHttpContext> GetContextAsync()
    {
        try
        {
            while (true)
            {
                var request = await requests.Reader.ReadAsync(closed.Token);
                if (!request.Aborted.IsCancellationRequested) return request;
                request.Close();
            }
        }
        catch (ChannelClosedException) { throw new ObjectDisposedException(nameof(WindowsLocalHttpListener)); }
    }
    public override void Close()
    {
        bool stop;
        lock (Gate)
        {
            if (disposed) return;
            disposed = true;
            host.Routes.Remove(prefix);
            stop = host.Routes.Count == 0;
            if (stop) Hosts.Remove(host.Port);
        }
        closed.Cancel(); requests.Writer.TryComplete();
        if (stop) host.Stop();
        // Pending handlers hold this token until their requests have unwound.
    }
    private async Task Handle(HttpContext http)
    {
        using var context = new Context(http);
        if (!requests.Writer.TryWrite(context)) { http.Response.StatusCode = 503; return; }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(closed.Token, http.RequestAborted);
        try
        {
            await context.Completed.Task.WaitAsync(cancellation.Token);
            http.Response.StatusCode = context.ResponseStatus;
            http.Response.ContentType = context.ResponseType;
            context.Body.Position = 0;
            await context.Body.CopyToAsync(http.Response.Body, cancellation.Token);
        }
        catch (OperationCanceledException) { http.Abort(); }
    }
    // Verify the actual socket, without invoking account or travel operations.
    internal static async Task VerifyAsync(int port)
    {
        Host host;
        lock (Gate)
            host = Hosts.GetValueOrDefault(port) ?? throw new IOException("Windows local listener is not registered.");
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}{host.ProbePath}");
        if (response.StatusCode != HttpStatusCode.NoContent)
            throw new IOException("Windows local listener did not pass its readiness check.");
    }

    // This embedded server belongs to the game session, not console/UI lifetime.
    private sealed class SessionHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Host
    {
        public readonly Dictionary<string, WindowsLocalHttpListener> Routes = new(StringComparer.Ordinal);
        private readonly WebApplication application;
        public int Port { get; }
        public string ProbePath { get; } = "/session-health/" + Guid.NewGuid().ToString("N");
        public Host(int port)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = [] });
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IHostLifetime>(new SessionHostLifetime());
            builder.WebHost.UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = 65536;
                options.Limits.MaxRequestHeadersTotalSize = 16384;
                options.Limits.MaxConcurrentConnections = 32;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
                options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(15);
                options.Listen(IPAddress.Loopback, port, listen => listen.Protocols = HttpProtocols.Http1);
            });
            application = builder.Build();
            application.Run(async context =>
            {
                if (context.Request.Method == "GET" && context.Request.Path == ProbePath)
                { context.Response.StatusCode = StatusCodes.Status204NoContent; return; }
                WindowsLocalHttpListener? listener;
                lock (Gate) listener = Routes.FirstOrDefault(pair => context.Request.Path.StartsWithSegments(pair.Key.TrimEnd('/'), StringComparison.Ordinal)).Value;
                if (listener == null) { context.Response.StatusCode = 404; return; }
                await listener.Handle(context);
            });
            try
            {
                application.StartAsync().GetAwaiter().GetResult();
                Port = new Uri(application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()).Port;
            }
            catch { application.DisposeAsync().AsTask().GetAwaiter().GetResult(); throw; }
        }
        public void Stop()
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); application.StopAsync(timeout.Token).GetAwaiter().GetResult(); }
            finally { application.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
    }
    private sealed class Context(HttpContext context) : LocalHttpContext, IDisposable
    {
        public readonly TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly CancellationToken Aborted = context.RequestAborted;
        public readonly MemoryStream Body = new();
        public int ResponseStatus = 200;
        public string? ResponseType;
        private readonly NameValueCollection headers = CopyHeaders(context.Request);
        private static NameValueCollection CopyHeaders(HttpRequest request)
        {
            var headers = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers) headers.Add(header.Key, header.Value.ToString());
            return headers;
        }
        public override string HttpMethod { get; } = context.Request.Method;
        public override string? Path { get; } = context.Request.Path.Value;
        public override NameValueCollection Headers => headers;
        public override string? RequestContentType { get; } = context.Request.ContentType;
        public override Stream InputStream { get; } = context.Request.Body;
        public override Stream OutputStream => Body;
        public override int StatusCode { set => ResponseStatus = value; }
        public override string ContentType { set => ResponseType = value; }
        public override void Close() => Completed.TrySetResult();
        public void Dispose() => Body.Dispose();
    }
}
