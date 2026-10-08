using System.Diagnostics;
namespace XIVLauncher.Common.Http;

public sealed record DownloadProgress(string Id, string File, long Received, long? Total, bool Active, bool? Succeeded = null);

// One operation-scoped observer follows async work and parallel downloads; never a global region event.
public sealed class DownloadScope : IDisposable
{
    private static readonly AsyncLocal<DownloadScope?> Slot = new();
    private readonly DownloadScope? previous;
    private readonly Action<DownloadProgress> report;
    public CancellationToken Token { get; }
    public static DownloadScope? Current => Slot.Value;
    public DownloadScope(Action<DownloadProgress> report, CancellationToken token)
    { previous = Slot.Value; this.report = report; Token = token; Slot.Value = this; }
    private readonly Dictionary<string, long> batchFiles = new();
    private long? batchTotal;
    public void BeginBatch(long total) { lock (batchFiles) { batchFiles.Clear(); batchTotal = total; } }
    public void ReportTotal(long received, long total) => report(new("__total", "", received, total, true));
    public void Report(DownloadProgress value)
    {
        report(value);
        lock (batchFiles)
        {
            if (batchTotal is not > 0) return;
            batchFiles[value.Id] = value.Received;
            ReportTotal(batchFiles.Values.Sum(), batchTotal.Value);
        }
    }
    public void Dispose() => Slot.Value = previous;
}

public static class UpdateDownload
{
    public static async Task FileAsync(HttpClient client, string url, string path, CancellationToken token = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, DownloadScope.Current?.Token ?? default);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(linked.Token);
        await using var output = System.IO.File.Create(path);
        await CopyAsync(input, output, Path.GetFileName(new Uri(url).AbsolutePath), response.Content.Headers.ContentLength, linked.Token);
    }
    public static async Task CopyAsync(Stream input, Stream output, string file, long? total, CancellationToken token, TimeSpan? idleTimeout = null)
    {
        var stallLimit = idleTimeout ?? TimeSpan.FromSeconds(60);
        if (stallLimit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        using var transfer = new DownloadTransfer(file, total);
        var buffer = new byte[128 * 1024];
        while (true)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            idle.CancelAfter(stallLimit);
            int count;
            try { count = await input.ReadAsync(buffer, idle.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException($"Download stalled for {stallLimit.TotalSeconds:g} seconds."); }
            if (count == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, count), token); transfer.Add(count);
        }
        if (total is >= 0 && transfer.Received != total) throw new IOException("Download ended before the advertised content length was received.");
        transfer.Complete();
    }
}

public sealed class DownloadTransfer : IDisposable
{
    private readonly DownloadScope? scope = DownloadScope.Current;
    private readonly string id = Guid.NewGuid().ToString("N");
    private readonly string file;
    private readonly long? total;
    private long received;
    private bool succeeded;
    public long Received => received;
    public void Complete() => succeeded = true;
    private long last;
    public DownloadTransfer(string file, long? total)
    { this.file = file; this.total = total; Report(true); }
    public void Add(long bytes)
    {
        scope?.Token.ThrowIfCancellationRequested(); received += bytes;
        if (Stopwatch.GetElapsedTime(last).TotalMilliseconds >= 100) Report(true);
    }
    private void Report(bool active)
    { last = Stopwatch.GetTimestamp(); scope?.Report(new(id, file, received, total, active, active ? null : succeeded)); }
    public void Dispose() => Report(false);
}
