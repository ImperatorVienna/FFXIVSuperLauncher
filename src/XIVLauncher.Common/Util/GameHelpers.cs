
namespace XIVLauncher.Common.Util;

public static class GameHelpers
{
    public static bool IsValidGamePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var gamePath = new DirectoryInfo(path);
        return File.Exists(Path.Combine(path, "game", "ffxiv_dx11.exe")) && !Repository.Ffxiv.IsBaseVer(gamePath);
    }
}
