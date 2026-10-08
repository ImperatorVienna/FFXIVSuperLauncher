using XIVLauncher.Linux.Taiwan;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public class TaiwanEntrypointTests
{
    [Fact] public async Task RejectsUnknownInjectorWithoutChangingOriginal()
    {
        var root=Path.Combine(Path.GetTempPath(),"tc-experiment-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try {
            var dll=Path.Combine(root,"Dalamud.Injector.dll");File.WriteAllText(dll,"unsupported build");
            await Assert.ThrowsAsync<IOException>(()=>TaiwanEntrypoint.PrepareAsync(new(Path.Combine(root,"Dalamud.Injector.exe")),root,Path.Combine(root,"missing-patch"),default));
            Assert.Equal("unsupported build",File.ReadAllText(dll));
            Assert.False(Directory.Exists(Path.Combine(root,"injection-cache")));
        } finally {Directory.Delete(root,true);}
    }
}
