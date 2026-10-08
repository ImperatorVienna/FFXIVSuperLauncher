using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public class RetiredLoginTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RemovedChinaLoginModesCannotBeDispatched(int retiredMode)
    {
        // These old numeric modes must fail before a request is sent; QR and session login remain covered by lifecycle tests.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new LoginClient().LoginAsync((LoginType)retiredMode, new LoginRequest()));
    }
}
