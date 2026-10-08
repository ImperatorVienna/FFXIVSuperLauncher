using System.Security.Cryptography;
namespace XIVLauncher.Linux;

public static class OneTimePassword
{
    public static bool IsManualCode(string value) => value.Length == 6 && value.All(c => c >= '0' && c <= '9');
    public static byte[] Decode(string input)
    {
        input = input.Replace(" ", "").Trim().TrimEnd('=').ToUpperInvariant();
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var c in input)
        {
            var n = alphabet.IndexOf(c); if (n < 0) throw new ArgumentException("2FA 密钥必须为 Base32 格式，不能填写六位验证码。");
            buffer = (buffer << 5) | n; bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); }
        }
        if (bytes.Count < 10 || (bits > 0 && (buffer & ((1 << bits) - 1)) != 0)) throw new ArgumentException("2FA 密钥无效。");
        return bytes.ToArray();
    }
    public static string Generate(string secret, DateTimeOffset time)
    {
        var bytes = Decode(secret);
        try
        {
            var counter = BitConverter.GetBytes(time.ToUnixTimeSeconds() / 30);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);
            var hash = HMACSHA1.HashData(bytes, counter); var offset = hash[^1] & 15;
            var value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
            return (value % 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

