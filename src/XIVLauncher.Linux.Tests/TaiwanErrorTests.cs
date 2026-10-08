using System.Text.Json;
using XIVLauncher.Linux.Taiwan;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class TaiwanErrorTests
{
    [Theory]
    [InlineData("error", "連線錯誤，請稍後再試。", "server-connection")]
    [InlineData("message", "帳號或密碼錯誤", "credentials")]
    [InlineData("msg", "reCAPTCHA 驗證失敗", "captcha")]
    public void ServerReasonIsVisibleButTechnicalLogRemainsEnglish(string field, string message, string reason)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string,string>{{field,message}}));
        var error = TaiwanLogin.Rejected("launcherLogin", json.RootElement);
        Assert.Contains(message,error.Message); Assert.Contains(reason,error.Diagnostic);
        Assert.DoesNotContain(message,DiagnosticLog.Format(error));
    }
    [Fact] public void ErrorMessagesRedactSubmittedSecretsAndNeverEchoOtherResponseFields()
    {
        var password="a-test-password!"; var email="person@example.test"; var otp="384920";
        var message=$"Validation failed {email} {password} {TaiwanLogin.EncodeCredential(password)} {otp} https://example.test/?token=hidden aReallyLongUnrelatedSecretToken";
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(new { error=new { message }, token="unrelated-session-secret" }));
        var error=TaiwanLogin.Rejected("launcherLogin",json.RootElement,email,password,otp);
        foreach(var secret in new[]{email,password,TaiwanLogin.EncodeCredential(password),otp,"hidden","aReallyLongUnrelatedSecretToken","unrelated-session-secret"})
        { Assert.DoesNotContain(secret,error.Message); Assert.DoesNotContain(secret,DiagnosticLog.Format(error)); }
        Assert.Contains("[redacted]",error.Message);
    }
    [Fact] public void UnknownResponseDoesNotDumpBody()
    {
        using var json=JsonDocument.Parse("{\"session\":\"private\",\"error\":[\"secret\"]}");
        var error=TaiwanLogin.Rejected("launcherLogin",json.RootElement);
        Assert.DoesNotContain("private",error.Message);Assert.DoesNotContain("secret",error.Message);
        Assert.Contains("服务端未提供",error.Message);
    }
}
