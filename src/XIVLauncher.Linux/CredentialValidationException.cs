namespace XIVLauncher.Linux;
public sealed class CredentialValidationException(string message, string userMessage) : IOException(message)
{
    public string UserMessage { get; } = userMessage;
}
