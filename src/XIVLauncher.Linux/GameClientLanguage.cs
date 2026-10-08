namespace XIVLauncher.Linux;

// Values match xl/src/XIVLauncher.Common/ClientLanguage.cs (German precedes French).
public enum InternationalClientLanguage { Japanese = 0, English = 1, German = 2, French = 3 }
public static class GameClientLanguage
{
    public static readonly (InternationalClientLanguage Value, string Name)[] Choices =
    [(InternationalClientLanguage.Japanese, "日本語"), (InternationalClientLanguage.English, "English"),
     (InternationalClientLanguage.French, "Français"), (InternationalClientLanguage.German, "Deutsch")];
    public static InternationalClientLanguage Validate(InternationalClientLanguage value) => Enum.IsDefined(value) ? value : InternationalClientLanguage.English;
    // Used by the international login/launch adapter. Other adapters keep their fixed language.
    public static int For(LinuxSettings settings) => settings.SelectedRegion == "ffxiv" ? (int)Validate(settings.Current.ClientLanguage) : 4;
}
