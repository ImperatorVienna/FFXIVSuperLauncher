using XIVLauncher.Common.Http;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release060Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super060-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact] public void InternationalClientLanguagePersistsAcrossRegionChangesAndRestart()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv";
        settings.Current.ClientLanguage = InternationalClientLanguage.German; settings.Save();
        settings.SelectedRegion = "ffxiv_cn"; settings.Current.ClientLanguage = InternationalClientLanguage.French; settings.Save();
        settings.SelectedRegion = "ffxiv_tc"; settings.Save();
        settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv";
        Assert.Equal(InternationalClientLanguage.German, settings.Current.ClientLanguage);
        Assert.Equal(2, GameClientLanguage.For(settings));
        settings.EnsureRegionAvailable();
        settings.SelectedRegion = "ffxiv_tc"; Assert.Equal(4, GameClientLanguage.For(settings));
    }
    [Fact] public void InternationalLanguageOrderUsesUpstreamValuesNotDisplayIndices()
    {
        Assert.Equal(new[] { 0, 1, 3, 2 }, GameClientLanguage.Choices.Select(c => (int)c.Value));
        Assert.Equal(InternationalClientLanguage.English, new RegionSettings().ClientLanguage);
        Assert.Equal(InternationalClientLanguage.English, GameClientLanguage.Validate((InternationalClientLanguage)9));
    }
    [Fact] public void RegionLabelsStaySeparateFromInternalIds()
    {
        Assert.Equal(new[] { "中国区", "繁中区", "国际区" }, LinuxSettings.Regions.Select(r => r.Name));
        Assert.Equal(new[] { "ffxiv_cn", "ffxiv_tc", "ffxiv" }, LinuxSettings.Regions.Select(r => r.Id));
        Assert.Equal("在不同游戏区服间同步", UiVocabulary.Normalize("在不同区服间同步"));
        Assert.Equal("繁中区客户端", UiVocabulary.Normalize("台服客户端"));
        Assert.Equal("语言和区服", UiVocabulary.Normalize("语言和区域"));
    }
    private static LauncherLog Log(int capacity = 1000) => new(capacity, () => new DateTimeOffset(2026,10,7,12,34,56,TimeSpan.Zero));
    [Fact] public void RepeatedSavesAppendTimestampedMessagesAndClearIsExplicit()
    {
        var log = Log(); log.Append("Settings saved.", "ffxiv_cn"); log.Append("Settings saved.", "ffxiv_cn"); log.Append("");
        Assert.Equal(2, log.Text.Split('\n').Length); Assert.Contains("[12:34:56] [INFO] [ffxiv_cn]", log.Text);
        log.Clear(); Assert.Empty(log.Text);
    }
    [Fact] public void DownloadUpdatesOneLineAndOnlyCompletesAtSuccess()
    {
        var log = Log(); log.Download("op", "Downloading patch", "ffxiv_tc", new("file", "test.patch", 0,100,true),false);
        Assert.Contains("0.0%",log.Text);
        for(var i=1;i<=100;i++) log.Download("op","Downloading patch","ffxiv_tc",new("file","test.patch",i,100,true),false);
        Assert.Single(log.Text.Split('\n')); Assert.DoesNotContain("100.0%",log.Text);
        log.Download("op","Downloading patch","ffxiv_tc",new("file","test.patch",100,100,false,true),false);
        Assert.Single(log.Text.Split('\n')); Assert.Contains("100.0%",log.Text); Assert.Contains("download complete",log.Text);
    }
    [Fact] public void UnknownLengthAndCancelledDownloadsNeverPretendToComplete()
    {
        var log=Log(); log.Download("op","Downloading Dalamud","ffxiv_cn",new("a","runtime.zip",10,null,true),false);
        Assert.Contains("size unknown",log.Text);
        log.Download("op","Downloading Dalamud","ffxiv_cn",new("a","runtime.zip",10,null,false,false),true);
        Assert.Contains("cancelled",log.Text); Assert.DoesNotContain("100.0%",log.Text);
        log.Download("op","Downloading Dalamud","ffxiv_cn",new("b","assets.zip",1,2,false,false),false);
        Assert.Equal(2,log.Text.Split('\n').Length); Assert.Contains("failed",log.Text);
    }
    [Fact] public async Task UnknownLengthSuccessfulDownloadReportsExplicitCompletion()
    {
        var events=new List<DownloadProgress>(); using var scope=new DownloadScope(events.Add,default);
        await UpdateDownload.CopyAsync(new MemoryStream(new byte[10]),new MemoryStream(),"test",null,default);
        Assert.True(events.Last().Succeeded); Assert.False(events.Last().Active);
    }
    [Fact] public async Task TruncatedDownloadDoesNotReportSuccess()
    {
        var events=new List<DownloadProgress>(); using var scope=new DownloadScope(events.Add,default);
        await Assert.ThrowsAsync<IOException>(()=>UpdateDownload.CopyAsync(new MemoryStream(new byte[10]),new MemoryStream(),"test",20,default));
        Assert.False(events.Last().Succeeded);
    }
    [Fact] public void BoundedLogRemovesStaleDownloadReferences()
    {
        var log=Log(2); log.Download("op","Downloading patch","ffxiv_tc",new("a","a.patch",0,10,true),false);
        log.Append("First"); log.Append("Second");
        log.Download("op","Downloading patch","ffxiv_tc",new("a","a.patch",5,10,true),false);
        Assert.Equal(2,log.Text.Split('\n').Length); Assert.Contains("50.0%",log.Text); Assert.DoesNotContain("First",log.Text);
    }
    [Theory]
    [InlineData("已读取 4 个大区。", "Loaded 4 data centers.")]
    [InlineData("检查 Dalamud：26-10-05-02", "Checking Dalamud: 26-10-05-02")]
    [InlineData("已应用，下次启动游戏生效。原设置备份：/tmp/backup", "Plugin states applied for the next game launch. Previous settings backup: /tmp/backup")]
    [InlineData("Taiwan authentication failed", "ffxiv_tc authentication failed")]
    public void StatusTemplatesProduceEnglishWithStableIds(string input,string expected) => Assert.Equal(expected,EnglishLog.Message(input));
}
