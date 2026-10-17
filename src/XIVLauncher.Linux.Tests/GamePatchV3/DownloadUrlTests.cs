using XIVLauncher.GamePatchV3.Update;
using Xunit;
namespace XIVLauncher.Linux.Tests.GamePatchV3;
public sealed class DownloadUrlTests
{
    [Theory]
    [InlineData("/patch/list.json")]
    [InlineData("patch/list.json")]
    public void RelativePathsUseBothCdnBases(string path)
    {
        var urls = GamePatchInstaller.BuildDownloadUrls(path, "https://primary.example/base", "https://backup.example/base");
        Assert.Equal(new[] { "primary.example", "backup.example" }, urls.Select(x => x.Host));
        Assert.All(urls, url => { Assert.Equal("https", url.Scheme); Assert.EndsWith("/base/patch/list.json", url.AbsolutePath); });
    }
    [Fact]
    public void AbsoluteHttpUrlRemainsAbsolute()
    {
        var urls = GamePatchInstaller.BuildDownloadUrls("https://cdn.example/list.json", "https://other.example");
        var url = Assert.Single(urls);
        Assert.Equal("https", url.Scheme); Assert.Equal("cdn.example", url.Host);
        Assert.EndsWith("/list.json", url.AbsolutePath);
    }
    [Theory]
    [InlineData("file:///tmp/list.json")]
    [InlineData("ftp://cdn.example/list.json")]
    public void UnsupportedSchemesAreRejected(string path)
        => Assert.Throws<InvalidDataException>(() => GamePatchInstaller.BuildDownloadUrls(path, "https://cdn.example"));
}
