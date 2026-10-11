using System.Reflection;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
namespace XIVLauncher.Linux;

// Only presentation properties are translated; identifiers, credentials and logs are untouched.
public static partial class Localization
{
    public static readonly (string Id, string Name)[] Languages = [("zh-CN", "简体中文"), ("zh-TW", "繁體中文"), ("ja", "日本語"), ("en", "English")];
    private static Dictionary<string, string> strings = new();
    public static string Language { get; private set; } = "zh-CN";
    public static IReadOnlyDictionary<string,string> Catalog(string language)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"XIVLauncher.Linux.Resources.Locales.{language}.json");
        return stream == null ? new Dictionary<string,string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
    }
    public static void SetLanguage(string language)
    {
        Language = Languages.Any(x => x.Id == language) ? language : "en";
        strings = new(Catalog(Language));
    }
    public static string T(string text) => UiVocabulary.Normalize(text);
    public static string F(FormattableString text) => string.Format(CultureInfo.CurrentCulture, T(text.Format), text.GetArguments());
    public static string Display(string text)
    {
        if (Languages.Any(x => x.Name == text)) return text;
        // Reviewed wording is final; do not normalize it back to the source vocabulary.
        if (strings.ContainsValue(text)) return text;
        text = UiVocabulary.Normalize(text);
        if (strings.TryGetValue(text, out var translated)) return translated;
        if (!Regex.IsMatch(text, "[\\u4e00-\\u9fff]")) return text;
        // Dynamic UI labels keep numbers, paths, player names and other values unchanged.
        foreach (var (key, value) in strings)
        {
            var slots = Regex.Matches(key, @"\{[^{}]+\}");
            if (slots.Count == 0) continue;
            var pattern = "^"; var last = 0;
            foreach (Match slot in slots) { pattern += Regex.Escape(key[last..slot.Index]) + "(.*?)"; last = slot.Index + slot.Length; }
            pattern += Regex.Escape(key[last..]) + "$";
            var match = Regex.Match(text, pattern, RegexOptions.Singleline, TimeSpan.FromMilliseconds(20));
            if (!match.Success) continue;
            var result = value;
            for (var i=0;i<slots.Count;i++) result = result.Replace(slots[i].Value, match.Groups[i+1].Value);
            return result;
        }
        foreach (var (key,value) in strings.OrderByDescending(x => x.Key.Length))
            if ((key.EndsWith('：') || key.EndsWith(":")) && text.StartsWith(key))
            {
                var suffix = strings.GetValueOrDefault(text[key.Length..], text[key.Length..]);
                var separator = Language == "en" && value.EndsWith(':') && suffix.Length > 0 && !char.IsWhiteSpace(suffix[0]) ? " " : "";
                return value + separator + suffix;
            }
        return text;
    }
}
