using System.Text.RegularExpressions;
namespace XIVLauncher.Linux;

// Covers prompts coming from the original shared components as well as launcher-owned labels.
public static class UiVocabulary
{
    public static string Normalize(string text)
    {
        text = text.Replace("区服和本地化", "语言和区服").Replace("语言和区域", "语言和区服")
            .Replace("语言和区服", "__REGION_TAB__")
            .Replace("简体中文-中国", "中国区").Replace("繁体中文-台湾", "繁中区")
            .Replace("台港澳新马区", "繁中区").Replace("繁体中文区", "繁中区")
            .Replace("国际服", "国际区").Replace("全球服", "国际区").Replace("国服", "中国区").Replace("台服", "繁中区")
            .Replace("客户端区服", "游戏区服");
        text = Regex.Replace(text, "(?<!游戏)区服", "游戏区服");
        return text.Replace("__REGION_TAB__", "语言和区服");
    }
}
