using Xunit;
namespace XIVLauncher.Linux.Tests;
public class WindowPlacementTests
{
    [Fact]
    public void WindowPreferenceRoundTripsWithoutWritingRegionOrCredentialFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "window-preference-" + Guid.NewGuid());
        try
        {
            var settings = LinuxSettings.Load(root);
            settings.WindowPlacement = new(-900, 40, 900, 750, true);
            settings.SaveGlobalPreferences();
            Assert.Equal(settings.WindowPlacement, LinuxSettings.Load(root).WindowPlacement);
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(-1, 700)]
    [InlineData(800, 0)]
    [InlineData(double.NaN, 700)]
    [InlineData(double.PositiveInfinity, 700)]
    public void RejectsInvalidSavedGeometry(double width, double height) => Assert.False(new WindowPlacement(0, 0, width, height, false).IsValid);
}
