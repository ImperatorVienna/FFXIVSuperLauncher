using System.Net.Http.Headers;

namespace XIVLauncher.Common.Util;

public static class APIHelper
{
    public static void AddWithoutValidation(this HttpHeaders headers, string key, string value)
    {
        if (!headers.TryAddWithoutValidation(key, value))
            throw new InvalidOperationException($"Could not add header - {key}: {value}");
    }
}
