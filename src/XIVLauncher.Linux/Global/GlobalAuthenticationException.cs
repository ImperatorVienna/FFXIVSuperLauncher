namespace XIVLauncher.Linux.Global;

// Only an explicitly extracted, redacted error callback is displayed, never the response page.
public sealed class GlobalAuthenticationException(string reason, bool steam, int? httpStatus, string? serverMessage = null)
    : IOException($"ffxiv authentication failed: stage=oauth-login; reason={reason}; mode={(steam ? "steam" : "standard")}; HTTP={httpStatus?.ToString() ?? "n/a"}.")
{
    public string? ServerMessage { get; } = serverMessage;
    public string UserMessage => Localization.Display(reason == "server-rejected" ? "国际区登录被服务端拒绝。" : "国际区登录响应格式异常，尚不能判定为账号、密码或验证码错误。")
        + (string.IsNullOrWhiteSpace(ServerMessage) ? Localization.Display("服务端未提供可显示的错误说明。") : " " + Localization.Display("服务端提示：") + ServerMessage);
}
