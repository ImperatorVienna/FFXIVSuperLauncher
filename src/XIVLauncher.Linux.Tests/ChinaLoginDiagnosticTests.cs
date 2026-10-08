using XIVLauncher.Login.Exceptions;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public class ChinaLoginDiagnosticTests
{
    [Fact] public void FirstDeviceLoginExplainsQrRequirement()
    {
        var error = new LoginException((int)LoginExceptionCode.FirstLoginOnDevice,"服务端本地化消息");
        var text=UserLogMessage.Failure(error);
        Assert.Contains("-10242296",text);Assert.Contains("QR-code",text);Assert.DoesNotContain("The operation failed",text);
    }
    [Fact] public void UnknownLoginFailureKeepsCodeWithoutLeakingServerText()
    {
        var error=new LoginException(-123,"synthetic-account-private-data");
        Assert.Contains("-123",DiagnosticLog.Format(error));
        Assert.DoesNotContain("synthetic-account-private-data",DiagnosticLog.Format(error));
    }
}
