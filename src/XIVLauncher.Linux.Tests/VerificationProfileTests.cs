using XIVLauncher.Linux.Taiwan;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class VerificationProfileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super-verification-" + Guid.NewGuid());
    [Fact] public void ProfileSurvivesSessionsAndHasExclusiveAccess()
    {
        var profile = Path.Combine(root, "captcha-browser");
        using (TaiwanCaptcha.OpenProfile(profile))
        {
            File.WriteAllText(Path.Combine(profile, "browser-state-fixture"), "saved");
            Assert.Throws<IOException>(() => TaiwanCaptcha.OpenProfile(profile));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(profile));
        }
        using (TaiwanCaptcha.OpenProfile(profile))
            Assert.Equal("saved", File.ReadAllText(Path.Combine(profile, "browser-state-fixture")));
    }
    [Fact] public void SymlinkProfileIsNotUsed()
    {
        Directory.CreateDirectory(root); var target = Path.Combine(root, "target"); Directory.CreateDirectory(target);
        var link = Path.Combine(root, "link"); Directory.CreateSymbolicLink(link, target);
        Assert.Throws<IOException>(() => TaiwanCaptcha.OpenProfile(link));
        Directory.Delete(link);
    }
    [Fact] public void GenericServiceErrorDoesNotAssertPasswordIsIncorrect()
    {
        var error = new TaiwanAuthenticationException("launcherLogin", 200, "server-connection", "連線錯誤，請稍後再試。");
        Assert.Contains("无法确定", error.Message);
        Assert.DoesNotContain("请检查账号、密码", error.Message);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
