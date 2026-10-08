using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace XIVLauncher.Linux;

public static class DiagnosticLog
{
    private static readonly IReadOnlyDictionary<string, string> Messages = Load();
    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = typeof(DiagnosticLog).Assembly.GetManifestResourceStream("XIVLauncher.Linux.Resources.Diagnostics.en.json");
        return stream == null ? new Dictionary<string, string>() : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
    // UI descriptions remain localized. Technical diagnostics use stable English labels and identifiers.
    public static string Format(Exception exception)
    {
        var output = new StringBuilder();
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(current.Message)))[..12];
            output.AppendLine($"Exception: {current.GetType().FullName}");
            output.AppendLine($"Diagnostic reference: {id}; HRESULT: 0x{current.HResult:X8}");
            if (current is HttpRequestException http && http.StatusCode is { } code) output.AppendLine($"HTTP status: {(int)code}");
            output.AppendLine(Summary(current));
            if (!string.IsNullOrEmpty(current.StackTrace)) output.AppendLine(current.StackTrace);
        }
        return output.ToString();
    }
    public static string Summary(Exception error) => EnglishLog.RegionIds(Describe(error));
    private static string Describe(Exception error)
    {
        if (error is XIVLauncher.Login.Exceptions.LoginException china)
            return china.ErrorCode == (int)XIVLauncher.Login.Exceptions.LoginExceptionCode.FirstLoginOnDevice
                ? $"ffxiv_cn authentication failed: code={china.ErrorCode}; first sign-in on this device requires QR-code login."
                : $"ffxiv_cn authentication failed: code={china.ErrorCode}; try QR-code login or check account verification in Daoyu.";
        if (error is Taiwan.TaiwanAuthenticationException authentication) return authentication.Diagnostic;
        if (Messages.TryGetValue(error.Message, out var translated)) return translated;
        foreach (var pair in Messages.Where(p => p.Key.EndsWith('：')).OrderByDescending(p => p.Key.Length))
            if (error.Message.StartsWith(pair.Key, StringComparison.Ordinal)) return pair.Value + error.Message[pair.Key.Length..];
        var http = Regex.Match(error.Message, @"^台服登录服务返回 HTTP (\d+)");
        if (http.Success) return "The Taiwan authentication service returned HTTP " + http.Groups[1].Value + ". Check credentials, OTP or retry later.";
        var runtime = Regex.Match(error.Message, @"^此 Proton 需要 Steam Linux Runtime (\d+)");
        if (runtime.Success) return "This Proton requires Steam Linux Runtime " + runtime.Groups[1].Value + ". Install it in Steam and refresh the tool list.";
        // External process output is preserved verbatim, including any locale it emits.
        if (error.Message.StartsWith("Injector ", StringComparison.Ordinal) || error.Message.StartsWith("Proton ", StringComparison.Ordinal)) return error.Message;
        if (!Regex.IsMatch(error.Message, @"[\p{IsCJKUnifiedIdeographs}\p{IsCJKSymbolsandPunctuation}]")) return error.Message;
        return error switch
        {
            FileNotFoundException => "A required file is missing. Check component installation and configured paths.",
            DirectoryNotFoundException => "A required directory is missing. Check the configured paths.",
            UnauthorizedAccessException => "Access was denied. Check file permissions.",
            NotSupportedException => "The selected operation or region is not supported.",
            InvalidDataException => "Input data or downloaded content failed validation.",
            ArgumentException => "A supplied setting or argument is invalid.",
            OperationCanceledException => "The operation was cancelled.",
            _ => "The operation failed. The diagnostic reference and stack identify the failure."
        };
    }
}
