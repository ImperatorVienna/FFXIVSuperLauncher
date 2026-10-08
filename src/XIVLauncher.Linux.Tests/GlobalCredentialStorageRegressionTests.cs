using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class GlobalCredentialStorageRegressionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "global-credentials-" + Guid.NewGuid());
    [Fact] public async Task ChinaAccountCanScanAndThenQuickLoginWithPlaintext()
    {
        var s = LinuxSettings.Load(root); s.Account = "old"; s.Current.RememberPassword = true;
        s.Save();
        s = LinuxSettings.Load(root);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var expected = attempt == 0 ? XIVLauncher.Login.Models.LoginType.QRCode : XIVLauncher.Login.Models.LoginType.QuickLogin;
            await ChinaLoginSession.AuthenticateAsync(s, new() { Account = "old", DeviceProfile = s.GetDeviceProfile("old") }, _ => {}, default,
                (method, request, token) =>
                {
                    Assert.Equal(expected, method);
                    return Task.FromResult(new XIVLauncher.Login.Client.LoginResult
                    { State = XIVLauncher.Login.Models.LoginState.Ok, OAuthLogin = new() { InputUserID = "old", QuickLoginSecret = "synthetic-quick" } });
                });
        }
        Assert.Equal("synthetic-quick", await RegionCredentials.ReadAsync(s, "old", "session", default));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
