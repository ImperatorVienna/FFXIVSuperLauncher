using System.Diagnostics;
using System.Reflection;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace XIVLauncher.Common.Util;

public static class PlatformHelpers
{

    public static string GetTempFileName() =>
        Path.Combine(Path.GetTempPath(), "xivlauncher_" + Guid.NewGuid());

    public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));

        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name));
    }

    public static void Unzip7ZAsset(string path, string output)
    {
        Log.Information("[DUPDATE] 正在解压 7z 包...");

        try
        {
            var unzipPath = "7z";

            var psi = new ProcessStartInfo
            {
                FileName        = unzipPath,
                Arguments       = $"x -y -bso0 -bsp0 -o\"{output}\" \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow  = true
            };

            var proc = Process.Start(psi);

            if (proc != null)
            {
                proc.WaitForExit();

                if (proc.ExitCode == 0)
                {
                    Log.Information("[DUPDATE] 7z 解压完成。");
                    return;
                }

                Log.Warning("[DUPDATE] 系统 7z 解压失败，退出码 {ExitCode}，回退到托管解压。", proc.ExitCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DUPDATE] 系统 7z 不可用，回退到托管解压。");
        }

        using (var archive = ArchiveFactory.OpenArchive(path))
            archive.WriteToDirectory(output, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        Log.Information("[DUPDATE] 托管解压完成。");
    }

    public static string GetVersion()
    {
        var assembly  = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var attribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute))!;
        var name      = (AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly,              typeof(AssemblyProductAttribute))!;
        Console.WriteLine(name?.Product + " v" + attribute?.InformationalVersion);
        return name?.Product + " v" + attribute?.InformationalVersion;
    }
}
