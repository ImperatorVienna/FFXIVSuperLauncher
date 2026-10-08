using XIVLauncher.DCTravel;
namespace XIVLauncher.Linux.ChinaTravel;
public static class TravelOrderMonitor
{
    public static async Task WaitAsync(Func<string, Task<DCTravelOrderInfo>> query, Func<string, bool, Task> confirm,
        string id, Action<string> progress, CancellationToken token, Func<CancellationToken, Task>? delay = null, Action? onSuccess = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(30));
        var ct = timeout.Token; var confirmed = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var info = await query(id).WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            switch (info.Status)
            {
                case DCTravelStatusType.Success: onSuccess?.Invoke(); return;
                case DCTravelStatusType.TravelFailed:
                case DCTravelStatusType.PreCheckFailed: throw new IOException("DC travel was rejected by the service. " + info.CheckMessage + " " + info.MigrationMessage);
                case DCTravelStatusType.NeedConfirmation:
                    progress("正在确认传送…"); if (!confirmed) { await confirm(id, true).WaitAsync(ct); confirmed = true; } break;
                case DCTravelStatusType.Checking:
                case DCTravelStatusType.CheckingAlt: progress("正在检查角色信息…"); break;
                default: progress("正在等待超域操作完成…"); break;
            }
            await (delay?.Invoke(ct) ?? Task.Delay(1000, ct));
        }
    }
}
