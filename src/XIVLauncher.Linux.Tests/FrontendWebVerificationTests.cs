using System.Text.Json;
using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class FrontendWebVerificationTests : IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),"frontend-verification-"+Guid.NewGuid());
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 [Fact] public async Task EmbeddedPageHasNoCredentialsAndStaysAliveThroughManualOtpAndAuthentication()
 {
  using var operation=new FrontendOperation(root,"ffxiv_tc");
  operation.Begin(async()=>{var result=await operation.AuthenticateWithWebAsync(async(value,token)=>{
   Assert.Equal("synthetic-captcha-token",value);
   var otp=await operation.AskAsync("otp","synthetic prompt",token:token);Assert.Equal("123456",otp);return "session-result";
  },operation.Token);Assert.Equal("session-result",result);});
  var view=await Wait(operation,"web-captcha");var id=view.Challenge!.Id;
  var page=JsonSerializer.SerializeToElement(operation.WebPage(id));
  Assert.Equal("https://launcher.ffxiv.com.tw/index.html",page.GetProperty("url").GetString());
  Assert.Contains("recaptcha/enterprise.js",page.GetProperty("html").GetString());
  Assert.DoesNotContain("synthetic-captcha-token",page.ToString());
  Assert.Throws<ArgumentException>(()=>operation.Reply(new(operation.Id,id,"123456")));
  Assert.Throws<ArgumentException>(()=>operation.WebReply("foreign", "synthetic-captcha-token"));
  operation.WebReply(id,"synthetic-captcha-token");
  var otpView=await Wait(operation,"otp");Assert.Equal(id,otpView.WebVerificationId);
  Assert.Throws<ArgumentException>(()=>operation.WebReply(id,"replayed-token"));
  operation.Reply(new(operation.Id,otpView.Challenge!.Id,"123456"));await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
  Assert.Equal("completed",operation.View().State);Assert.Null(operation.View().WebVerificationId);
  Assert.DoesNotContain("synthetic-captcha-token",operation.View().Log);Assert.DoesNotContain("123456",operation.View().Log);
  Assert.Throws<ArgumentException>(()=>operation.WebPage(id));
 }
 [Fact] public async Task ClosingVerificationCancelsTheWaitWithoutBlockingLaterWork()
 {
  using var operation=new FrontendOperation(root,"ffxiv_tc");
  operation.Begin(async()=>{await operation.AuthenticateWithWebAsync((_,_)=>throw new Exception("must not authenticate"),operation.Token);});
  await Wait(operation,"web-captcha");operation.Cancel();await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
  Assert.Equal("cancelled",operation.View().State);Assert.Null(operation.View().WebVerificationId);
 }
 [Fact] public async Task PageFailureReportsOnlyAnAllowedStageAndReleasesAuthentication()
 {
  using var operation=new FrontendOperation(root,"ffxiv_tc");
  operation.Begin(async()=>{await operation.AuthenticateWithWebAsync((_,_)=>throw new Exception("must not authenticate"),operation.Token);});
  var view=await Wait(operation,"web-captcha");var id=view.Challenge!.Id;
  Assert.Throws<ArgumentException>(()=>operation.WebFail(id,"synthetic-secret"));
  Assert.Throws<ArgumentException>(()=>operation.WebFail("stale","page-initialization"));
  operation.WebFail(id,"page-initialization");await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
  Assert.Equal("failed",operation.View().State);Assert.Null(operation.View().WebVerificationId);
  Assert.Contains("Web verification failed: page-initialization.",operation.View().Log);
  Assert.DoesNotContain("synthetic-secret",operation.View().Log);
 }
 private static async Task<OperationView> Wait(FrontendOperation operation,string kind)
 {for(var i=0;i<500;i++){var view=operation.View();if(view.Challenge?.Kind==kind)return view;await Task.Delay(5);}throw new TimeoutException();}
}
