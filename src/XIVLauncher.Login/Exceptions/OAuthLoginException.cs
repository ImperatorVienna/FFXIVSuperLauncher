
namespace XIVLauncher.Login.Exceptions;

[Serializable]
public partial class OAuthLoginException
(
    string? document
) : Exception(document ?? "未知错误")
{
    public string? OAuthErrorMessage { get; private set; } = document;
}
