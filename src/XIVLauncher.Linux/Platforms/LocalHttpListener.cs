using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;

namespace XIVLauncher.Linux.Platforms;

// Only transport varies by platform. RPC validation and session ownership stay shared.
internal abstract class LocalHttpListener
{
    public abstract int Port { get; }
    public abstract Task<LocalHttpContext> GetContextAsync();
    public abstract void Close();
    public static LocalHttpListener Listen(int port, string prefix)
    {
#if WINDOWS_BACKEND
        return Windows.WindowsLocalHttpListener.Listen(port, prefix);
#else
        return new ManagedListener(port, prefix);
#endif
    }

    private sealed class ManagedListener : LocalHttpListener
    {
        private readonly HttpListener listener = new();
        public override int Port { get; }
        public ManagedListener(int port, string prefix)
        {
            try
            {
                for (var attempt = 0; ; attempt++)
                {
                    if (port == 0)
                    {
                        using var reservation = new TcpListener(IPAddress.Loopback, 0);
                        reservation.Start(); Port = ((IPEndPoint)reservation.LocalEndpoint).Port;
                    }
                    else Port = port;
                    listener.Prefixes.Clear();
                    listener.Prefixes.Add($"http://127.0.0.1:{Port}{prefix}");
                    try { listener.Start(); break; }
                    catch (HttpListenerException) when (port == 0 && attempt < 9) { }
                }
            }
            catch { listener.Close(); throw; }
        }
        public override async Task<LocalHttpContext> GetContextAsync() => new ManagedContext(await listener.GetContextAsync());
        public override void Close() => listener.Close();
    }
    private sealed class ManagedContext(HttpListenerContext context) : LocalHttpContext
    {
        public override string HttpMethod => context.Request.HttpMethod;
        public override string? Path => context.Request.Url?.AbsolutePath;
        public override NameValueCollection Headers => context.Request.Headers;
        public override string? RequestContentType => context.Request.ContentType;
        public override Stream InputStream => context.Request.InputStream;
        public override Stream OutputStream => context.Response.OutputStream;
        public override int StatusCode { set => context.Response.StatusCode = value; }
        public override string ContentType { set => context.Response.ContentType = value; }
        public override void Close() => context.Response.Close();
    }
}

internal abstract class LocalHttpContext
{
    public abstract string HttpMethod { get; }
    public abstract string? Path { get; }
    public abstract NameValueCollection Headers { get; }
    public abstract string? RequestContentType { get; }
    public abstract Stream InputStream { get; }
    public abstract Stream OutputStream { get; }
    public abstract int StatusCode { set; }
    public abstract string ContentType { set; }
    public abstract void Close();
}
