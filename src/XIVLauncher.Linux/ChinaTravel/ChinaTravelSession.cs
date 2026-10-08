using XIVLauncher.DCTravel;
using XIVLauncher.Login.Workflow;
namespace XIVLauncher.Linux.ChinaTravel;

public sealed class ChinaTravelSession : ILoginSessionRefreshSink, IDisposable
{
    public DCTravelClient Client { get; } = new("");
    public bool Authenticated { get; private set; }
    public string Account { get; set; } = "";
    public void Bind(LoginSessionRefreshContext context) { Client.BindLoginSessionRefresh(context); Authenticated = true; }
    public void Dispose() => Client.Dispose();
}
