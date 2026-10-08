using System.Diagnostics;
using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux;
public static class RegionLaunchPreparation
{
    public static RegionDalamudFiles FixedChinaHelper(string baseDirectory)
    {
        var root = Path.Combine(baseDirectory, "Tools/cn-launch-support");
        var injector = new FileInfo(Path.Combine(root, "injector/Dalamud.Injector.exe"));
        var runtime = new DirectoryInfo(Path.Combine(root, "runtime"));
        if (!injector.Exists || !File.Exists(Path.Combine(runtime.FullName, "host/fxr/10.0.1/hostfxr.dll")))
            throw new IOException("随包提供的国服启动辅助组件缺失，请重新安装完整发行包；未尝试下载或更新 Dalamud。");
        return new(injector, runtime, new DirectoryInfo(Path.Combine(root, "injector")), 4, true);
    }
    public static async Task LaunchPlainAsync(ProcessStartInfo psi, string arguments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        psi.Arguments = string.Join(' ', psi.ArgumentList.Select(UnixDalamudRunner.QuoteArgument)) + " " + arguments;
        psi.ArgumentList.Clear();
        var process = new Process { StartInfo = psi };
        if (!process.Start()) { process.Dispose(); throw new IOException("无法启动游戏。"); }
        var output = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        var completion = Task.Run(async () =>
        {
            try { await process.WaitForExitAsync(); await Task.WhenAll(output, errors); return process.ExitCode; }
            finally { process.Dispose(); }
        });
        if (await Task.WhenAny(completion, Task.Delay(1000, token)) == completion && await completion != 0)
            throw new IOException("Proton 游戏启动进程异常退出。请检查客户端路径及兼容环境。");
        token.ThrowIfCancellationRequested();
        // Proton's Unix PID is not a Wine PID; don't report injection confirmation here.
    }
}
