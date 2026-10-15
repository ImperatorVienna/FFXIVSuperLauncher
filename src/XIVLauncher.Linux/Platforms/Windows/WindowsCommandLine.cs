using System.Text;
namespace XIVLauncher.Linux.Platforms.Windows;

internal static class WindowsCommandLine
{
    internal static string Quote(string value)
    {
        var result = new StringBuilder("\""); var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            slashes = 0; result.Append(c);
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
