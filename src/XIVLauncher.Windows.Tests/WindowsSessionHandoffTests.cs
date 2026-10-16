using XIVLauncher.Linux.Platforms.Windows;
using Xunit;

namespace XIVLauncher.Windows.Tests;

public sealed class WindowsSessionHandoffTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("42")]
    [InlineData("100 invalid")]
    [InlineData("100 a b")]
    public void InvalidOrSelfProcessIsRejected(string request) =>
        Assert.Throws<IOException>(() => WindowsSessionHandoff.Parse(request, 42));

    [Fact]
    public void BackendCannotMasqueradeAsPrimary()
    {
        var request = WindowsSessionHandoff.Parse("100", 42);
        request.Validate("backend.exe", "backend.exe", "launcher.exe", null);
        Assert.Throws<IOException>(() => request.Validate("launcher.exe", "backend.exe", "launcher.exe", null));
    }

    [Fact]
    public void PrimaryRequiresMatchingImagePidAndSessionToken()
    {
        var id = Guid.NewGuid().ToString();
        var request = WindowsSessionHandoff.Parse("100 " + id, 42);
        var discovery = "{\"pid\":100,\"id\":\"" + id + "\"}";
        request.Validate("launcher.exe", "backend.exe", "launcher.exe", discovery);
        Assert.Throws<IOException>(() => request.Validate("unrelated.exe", "backend.exe", "launcher.exe", discovery));
        Assert.Throws<IOException>(() => request.Validate("launcher.exe", "backend.exe", "launcher.exe", discovery.Replace("100", "101")));
        Assert.Throws<IOException>(() => request.Validate("launcher.exe", "backend.exe", "launcher.exe", discovery.Replace(id, Guid.NewGuid().ToString())));
    }
}
