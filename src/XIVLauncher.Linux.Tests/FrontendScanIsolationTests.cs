using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class FrontendScanIsolationTests
{
    private sealed class Input : TextReader
    {
        internal readonly Channel<string> Lines=Channel.CreateUnbounded<string>();
        public override async ValueTask<string?> ReadLineAsync(CancellationToken token)
            => await Lines.Reader.WaitToReadAsync(token)?await Lines.Reader.ReadAsync(token):null;
    }
    private sealed class Output : TextWriter
    {
        public override Encoding Encoding=>Encoding.UTF8;
        internal readonly Channel<string> Lines=Channel.CreateUnbounded<string>();
        public override Task WriteLineAsync(string? value){Lines.Writer.TryWrite(value!);return Task.CompletedTask;}
        public override Task FlushAsync(CancellationToken token)=>Task.CompletedTask;
        internal async Task<JsonElement> Next()=>JsonSerializer.Deserialize<JsonElement>(await Lines.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
    }
    [Fact] public async Task StalledScanDoesNotBlockCommandsOrSpawnUnboundedRetries()
    {
        var root=Path.Combine(Path.GetTempPath(),"scan-isolation-"+Guid.NewGuid());
        using var release=new ManualResetEventSlim();
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int scans=0;var input=new Input();var output=new Output();
        CompatibilityView Scan(){Interlocked.Increment(ref scans);started.TrySetResult();release.Wait();finished.TrySetResult();return new("","","",false,"",false,false,"",[],[]);}
        var host=FrontendHost.RunAsync(root,input,output,scanCompatibility:Scan,scanDeadline:TimeSpan.FromMilliseconds(300));
        async Task Send(string id,string method)=>await input.Lines.Writer.WriteAsync(JsonSerializer.Serialize(new{id,method,@params=new{}}));
        try
        {
            await Send("scan","compatibility.read");await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Send("snapshot","snapshot");var snapshot=await output.Next();Assert.Equal("snapshot",snapshot.GetProperty("id").GetString());Assert.True(snapshot.TryGetProperty("result",out _));
            var timeout=await output.Next();Assert.Equal("read-timeout",timeout.GetProperty("error").GetProperty("code").GetString());
            await Send("retry","compatibility.read");var retry=await output.Next();Assert.Equal("operation-busy",retry.GetProperty("error").GetProperty("code").GetString());Assert.Equal(1,scans);
            await Send("after","snapshot");Assert.True((await output.Next()).TryGetProperty("result",out _));
            release.Set();await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // The original worker must exit before the next scan may begin.
            for(var i=0;i<100;i++)
            {
                await Send("recovered","compatibility.read");var result=await output.Next();
                if(result.TryGetProperty("result",out _))break;
                await Task.Delay(10);
            }
            Assert.Equal(2,scans);
        }
        finally{release.Set();input.Lines.Writer.TryComplete();await host.WaitAsync(TimeSpan.FromSeconds(5));if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
