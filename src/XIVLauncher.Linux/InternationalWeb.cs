namespace XIVLauncher.Linux;

// Website preferences never replace the account region returned by authentication.
public static class InternationalWeb
{
    public static readonly string[] CdKeyVersions = ["JP", "NA", "EU"];
    public static readonly (string Value, string Name)[] EuLanguages =
        [("en-gb", "English(UK)"), ("fr-fr", "Français"), ("de-de", "Deutsch")];
    // Older settings used US; normalize both saved accounts and the active profile.
    public static string NormalizeCdKeyVersion(string? version) => version is "JP" or "EU" ? version : "NA";
    public static (string Site, string Locale, string NewsLanguage) Preferences(string version, string euLanguage) =>
        NormalizeCdKeyVersion(version) switch
        {
            "JP" => ("jp", "ja-jp", "ja"),
            "EU" => euLanguage switch
            {
                "fr-fr" => ("fr", "fr-fr", "fr"),
                "de-de" => ("de", "de-de", "de"),
                _ => ("eu", "en-gb", "en-gb")
            },
            _ => ("na", "en-us", "en-us")
        };
    public static OfficialLinks Links(string version, string euLanguage)
    {
        var (site, locale, _) = Preferences(version, euLanguage);
        // Mog Station has one shared service. The login locale selects its presentation;
        // billing and entitlement remain controlled by the authenticated Square Enix account.
        var mog = "https://secure.square-enix.com/oauth/oa/oauthlogin?response_type=code&client_id=ffxiv_mog&redirect_uri="
            + Uri.EscapeDataString("https://secure.square-enix.com/account/app/svc/top?request=mogstation") + "&lang=" + locale;
        return new($"https://{site}.finalfantasyxiv.com/lodestone/",
            $"https://store.finalfantasyxiv.com/ffxivstore/{locale}/", null, mog);
    }
}
