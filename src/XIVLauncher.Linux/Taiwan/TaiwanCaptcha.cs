using System.Text.Json;

namespace XIVLauncher.Linux.Taiwan;

/// <summary>Linux browser equivalent of xl_tw's WebView2 page. The browser sees only CAPTCHA, never account credentials.</summary>
public static class TaiwanCaptcha
{
    public const string PageUrl = "https://launcher.ffxiv.com.tw/index.html";
    public const string SiteKey = "6Ld6VmorAAAAANQdQeqkaOeScR42qHC7Hyalq00r";
    internal static string CreatePage(string binding)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(binding, "^superCaptcha[a-f0-9]{32}$")) throw new ArgumentException("Invalid verification binding.");
        return "<!doctype html><html lang=zh-CN><meta charset=utf-8><title>" + System.Net.WebUtility.HtmlEncode(Localization.Display("FFXIV 台服验证")) + "</title>" +
                            "<body style='font:14px serif;margin:8px;background:#151a1d;color:#f4e7d2'><h2>" + System.Net.WebUtility.HtmlEncode(Localization.Display("台服登录验证")) + "</h2><p id=s>" + System.Net.WebUtility.HtmlEncode(Localization.Display("正在执行官方网页验证…")) + "</p>" +
                            "<button onclick='location.reload()'>" + System.Net.WebUtility.HtmlEncode(Localization.Display("重试验证")) + "</button><script src='https://www.google.com/recaptcha/enterprise.js?render=" + SiteKey + "'></script><script>" +
                            "grecaptcha.enterprise.ready(async()=>{try{const t=await grecaptcha.enterprise.execute('" + SiteKey + "',{action:'LOGIN'});window['" + binding + "'](t);document.getElementById('s').textContent=" + JsonSerializer.Serialize(Localization.Display("验证完成，正在返回启动器")) + ";}catch(e){document.getElementById('s').textContent=" + JsonSerializer.Serialize(Localization.Display("验证未完成，请重试或关闭窗口取消。")) + ";}});</script></body></html>";
    }
}
