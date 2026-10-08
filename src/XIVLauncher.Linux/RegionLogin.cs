using System.Text;
using System.Text.Json;
using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Login.Channels;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;
using XIVLauncher.Linux.Taiwan;
namespace XIVLauncher.Linux;

public sealed record RegionLoginContext(LinuxSettings Settings, LoginArea? Area, LoginArea[] Areas,
    LoginRequest Request, string Password, string ManualOtp, Func<CancellationToken, Task<string>> Captcha,
    Action<string> Status, Func<Func<string, CancellationToken, Task<string>>, CancellationToken, Task<string>>? AuthenticateWithCaptcha = null, Func<CancellationToken, Task<string>>? RequestManualOtp = null, Func<CancellationToken, Task>? UpdateAfterLogin = null);
public interface IRegionLogin
{
    Task<LoginArea[]> AreasAsync(CancellationToken token);
    Task<string> AuthenticateAsync(RegionLoginContext context, CancellationToken token);
}
public static class RegionLogin
{
    // No other region is initialized as a side effect of selecting one provider.
    public static IRegionLogin For(string region) => region switch
    {
        "ffxiv_cn" => new ChinaLogin(), "ffxiv_tc" => new TraditionalChineseLogin(),
        "ffxiv" => new Global.GlobalLogin(RegionBackends.GlobalHttp),
        _ => throw new NotSupportedException("未知区服。")
    };
    private sealed class ChinaLogin : IRegionLogin
    {
        public async Task<LoginArea[]> AreasAsync(CancellationToken token) => (await LoginArea.Get().WaitAsync(token)).ToArray();
        public async Task<string> AuthenticateAsync(RegionLoginContext context, CancellationToken token)
        {
            var result = await Task.Run(() => ChinaLoginSession.AuthenticateAsync(context.Settings, context.Request, context.Status, token), token);
            if (result.State != LoginState.Ok || result.OAuthLogin is not { } oauth) throw new IOException("登录未成功：" + result.State);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(oauth.TGT) || string.IsNullOrEmpty(oauth.Guid)) throw new IOException("登录响应缺少会话票据。");
            if (context.UpdateAfterLogin != null) await context.UpdateAfterLogin(token);
            var sid = await new LoginChannelContext(context.Request.DeviceProfile).GetSessionIdAsync(oauth.TGT, oauth.Guid).WaitAsync(token);
            var area = context.Area ?? throw new IOException("请选择大区。");
            return new Launcher().CreateGameStartRequest(sid, oauth.SndaID, 0, area.AreaID, area.AreaLobby, area.AreaGM,
                area.AreaConfigUpload, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(context.Areas))), "",
                new DirectoryInfo(context.Settings.GamePath)).Arguments;
        }
    }
    private sealed class TraditionalChineseLogin : IRegionLogin
    {
        public Task<LoginArea[]> AreasAsync(CancellationToken token) => Task.FromResult(Array.Empty<LoginArea>());
        public async Task<string> AuthenticateAsync(RegionLoginContext context, CancellationToken token)
        {
            var settings = context.Settings;
            if (string.IsNullOrWhiteSpace(settings.Account) || string.IsNullOrEmpty(context.Password)) throw new IOException("请输入台服账号和密码。");
            var otpSecret = await LoginCode.SavedSecretForLoginAsync(settings, token);
            context.Status("正在打开网页验证，请完成验证；关闭验证窗口可取消。");
            async Task<string> Authenticate(string captcha, CancellationToken verificationToken)
            {
                var otp = await LoginCode.GetAsync(otpSecret, context.RequestManualOtp ?? (_ => Task.FromResult(context.ManualOtp)), verificationToken);
                if (otp.Length != 0 && (otp.Length != 6 || otp.Any(c => c < '0' || c > '9'))) throw new IOException("2FA 验证码必须为六位数字。");
                context.Status("正在验证账号…");
                return await new TaiwanLogin(RegionBackends.TaiwanHttp).AuthenticateAsync(settings.Account, context.Password, otp, captcha, verificationToken);
            }
            var loginToken = context.AuthenticateWithCaptcha != null
                ? await context.AuthenticateWithCaptcha(Authenticate, token)
                : await Authenticate(await context.Captcha(token), token);
            if (context.UpdateAfterLogin != null) await context.UpdateAfterLogin(token);
            var sid = await new TaiwanLogin(RegionBackends.TaiwanHttp).CompleteAsync(settings.GamePath, loginToken, token);
            return TaiwanLogin.GameArguments(sid);
        }
    }
}
