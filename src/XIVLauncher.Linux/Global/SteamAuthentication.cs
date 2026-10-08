using System.Runtime.InteropServices;
using Steamworks;
using XIVLauncher.Common.Encryption;
namespace XIVLauncher.Linux.Global;

// Linux Steam initialization and ticket encryption follow xl's UnixSteam and Ticket.
// Keep Steam alive through authentication; do not request or save the Steam password.
public sealed class SteamAuthentication : IDisposable
{
    [DllImport("libc", SetLastError = true)] private static extern int setenv(string name, string value, int overwrite);
    private bool initialized;
    public async Task<Ticket> GetAsync(bool freeTrial, CancellationToken token)
    {
        var app = freeTrial ? 312060u : 39210u;
        setenv("SteamAppId", app.ToString(), 1); setenv("SteamGameId", app.ToString(), 1);
        try { SteamClient.Init(app); initialized = true; }
        catch (Exception ex) { throw new IOException("ffxiv Steam initialization failed. Start Steam, sign in online, then retry.", ex); }
        if (!SteamClient.IsValid || !SteamClient.IsLoggedOn) throw new IOException("ffxiv Steam must be running and signed in online.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var ticket = await SteamUser.GetAuthSessionTicketAsync().WaitAsync(TimeSpan.FromSeconds(30), token);
            if (ticket?.Data is { Length: > 0 } bytes)
                return Ticket.EncryptAuthSessionTicket(bytes, (uint)((DateTimeOffset)SteamUtils.SteamServerTime).ToUnixTimeSeconds());
        }
        throw new IOException("ffxiv Steam did not return an authentication ticket.");
    }
    public void Dispose() { if (initialized) SteamClient.Shutdown(); }
}
