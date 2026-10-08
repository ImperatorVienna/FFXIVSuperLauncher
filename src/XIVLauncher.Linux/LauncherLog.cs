using System.Text.RegularExpressions;
namespace XIVLauncher.Linux;

// Session-only bounded log. Appending the same message twice still records both actions.
public sealed class LauncherLog(int capacity = 1000, Func<DateTimeOffset>? clock = null)
{
    private sealed class Entry(string text, string? id = null) { public string Text = text; public string? Id = id; }
    private readonly LinkedList<Entry> entries = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> downloads = new();
    private readonly object gate = new();
    public string Text { get { lock (gate) return string.Join(Environment.NewLine, entries.Select(e => e.Text)); } }
    public event Action? Changed;
    private string Stamp(string level, string region) => $"[{(clock?.Invoke() ?? DateTimeOffset.Now):HH:mm:ss}] [{level}] [{region}] ";
    private void Trim()
    {
        while (entries.Count > capacity && entries.First != null)
        { if (entries.First.Value.Id is { } id) downloads.Remove(id); entries.RemoveFirst(); }
    }
    public void Append(string? message, string region = "shared", string level = "INFO")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        var stamp = Stamp(level, region);
        lock (gate)
        {
            foreach (var line in EnglishLog.Message(message).Replace("\r", "").Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)))
                entries.AddLast(new Entry(stamp + line));
            Trim();
        }
        Changed?.Invoke();
    }
    public void Download(string operation, string label, string region, XIVLauncher.Common.Http.DownloadProgress value, bool cancelled)
    {
        if (value.Id == "__total") return; // Each file has its own percentage; no synthetic unknown total.
        var id = operation + ":" + value.Id;
        var percent = value.Succeeded == true ? "100.0%" : value.Total is > 0
            ? Math.Clamp(100d * value.Received / value.Total.Value, 0, 99.9).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : value.Received == 0 ? "0.0%" : "size unknown";
        var state = value.Active ? "" : value.Succeeded == true ? " (download complete)" : cancelled ? " (cancelled)" : " (failed)";
        var text = Stamp("DOWNLOAD", region) + label + ": " + value.File.Replace("\r", " ").Replace("\n", " ") + " — " + percent +
            $" ({value.Received / 1048576d:F1} MiB)" + state;
        lock (gate)
        {
            if (downloads.TryGetValue(id, out var node)) node.Value.Text = text;
            else { node = entries.AddLast(new Entry(text, id)); downloads[id] = node; }
            Trim();
        }
        Changed?.Invoke();
    }
    public void Clear() { lock (gate) { entries.Clear(); downloads.Clear(); } Changed?.Invoke(); }
}

internal sealed class LogTarget(Action<string> append)
{
    public string? Text { set { if (!string.IsNullOrWhiteSpace(value)) append(value); } }
}
public static class EnglishLog
{
    private static readonly IReadOnlyDictionary<string, string> Messages = Load();
    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = typeof(EnglishLog).Assembly.GetManifestResourceStream("XIVLauncher.Linux.Resources.Status.en.json");
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(stream!)!;
    }
    public static string RegionIds(string value) => Regex.Replace(value, @"(?<![\w./\\])(?:Taiwan|China|Chinese region|Global region|Global)(?![\w/\\])", m => m.Value switch { "Taiwan" => "ffxiv_tc", "China" or "Chinese region" => "ffxiv_cn", _ => "ffxiv" });
    public static string Message(string value)
    {
        if (value.StartsWith("Exception:") || value.StartsWith("Dalamud log directory:") || value.StartsWith("Backup path:")) return RegionIds(value);
        if (value.Contains('\n')) return string.Join("\n", value.Split('\n').Select(Message));
        foreach (var (prefix, english) in new[] { ("无法读取插件设置：", "Cannot read plugin settings: "), ("应用失败：", "Apply failed: ") })
            if (value.StartsWith(prefix)) return english + DiagnosticLog.Summary(new IOException(value[prefix.Length..]));
        if (Messages.TryGetValue(value, out var exact)) return RegionIds(exact);
        // Translation templates use numbered placeholders; substitute values without translating paths/names.
        foreach (var pair in Messages.Where(p => p.Key.Contains('{')))
        {
            var indexes = Regex.Matches(pair.Key, @"\{(\d+)\}").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
            var pattern = "^" + Regex.Replace(Regex.Escape(pair.Key), @"\\\{(\d+)}", "(.*?)") + "$";
            var match = Regex.Match(value, pattern, RegexOptions.Singleline);
            if (!match.Success) continue;
            var result = pair.Value;
            for (var i = 0; i < indexes.Length; i++) result = result.Replace("{" + indexes[i] + "}", match.Groups[i+1].Value);
            return RegionIds(Regex.Replace(result, @"（更新包 (\d+)/(\d+)）", " (package $1/$2)"));
        }
        if (value.StartsWith("Exception:") || value.StartsWith("Dalamud log directory:") || value.StartsWith("Backup path:")) return RegionIds(value);
        if (!Regex.IsMatch(value, @"[\p{IsCJKUnifiedIdeographs}]")) return RegionIds(value);
        // Known backend errors share the technical diagnostic catalog. Never turn raw secrets into logs.
        var summary = DiagnosticLog.Summary(new IOException(value));
        if (summary.StartsWith("The operation failed. The diagnostic reference"))
            return "An untranslated component message was received (reference " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..12] + ").";
        return summary;
    }
}
