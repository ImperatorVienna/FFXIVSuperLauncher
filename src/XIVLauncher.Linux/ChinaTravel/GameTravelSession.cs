using XIVLauncher.Login.Models;
namespace XIVLauncher.Linux.ChinaTravel;

// Sole owner of the game-lifetime authentication and both local protocol endpoints.
public sealed class GameTravelSession : IAsyncDisposable
{
    private readonly GameSessionAuthentication authentication;
    private readonly DcTravelerBridge bridge;
    private readonly GameRecoveryEndpoint recovery;
    private int disposed;
    public int Port => bridge.Port;
    private GameTravelSession(GameSessionAuthentication authentication, DcTravelerBridge bridge, GameRecoveryEndpoint recovery)
    { this.authentication=authentication; this.bridge=bridge; this.recovery=recovery; }
    internal static async Task<GameTravelSession> CreateAsync(string root, GameSessionAuthentication authentication, LoginArea[] areas, Action<string> setArea)
    {
        DcTravelerBridge? bridge=null;
        try
        {
            var diagnostics=new SessionDiagnostics(root);
            bridge=new DcTravelerBridge(authentication.Refresh, areas, setArea,
                (message,level)=>diagnostics.Write(message,"ffxiv_cn",level));
            return new(authentication, bridge, new GameRecoveryEndpoint(root,bridge.Port,authentication));
        }
        catch
        {
            try { if(bridge!=null)await bridge.DisposeAsync(); }
            finally { await authentication.DisposeAsync(); }
            throw;
        }
    }
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        try { await recovery.DisposeAsync(); }
        finally
        {
            try { await authentication.DisposeAsync(); }
            finally { await bridge.DisposeAsync(); }
        }
    }
}
