using Xunit;
namespace XIVLauncher.Linux.Tests;
public class LocalizationReviewTests
{
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    [InlineData("en")]
    [InlineData("ja")]
    public void EveryReviewedResourceIsReachableByItsOriginalKey(string language)
    {
        try
        {
            Localization.SetLanguage(language);
            foreach (var (key, value) in Localization.Catalog(language))
                Assert.Equal(value, Localization.Display(key));
        }
        finally { Localization.SetLanguage("zh-CN"); }
    }
    [Fact]
    public void ReviewedChineseStaticAndDynamicWordingIsAppliedWithoutRenormalizing()
    {
        try
        {
            Localization.SetLanguage("zh-CN");
            Assert.Equal("Dalamud 和插件", Localization.Display("Dalamud和插件"));
            const string reviewed = "请点击放弃修改，重新读取当前区服的插件列表。";
            Assert.Equal(reviewed, Localization.Display("请点击放弃修改，重新读取当前游戏区服的插件列表。"));
            Assert.Equal(reviewed, Localization.Display(reviewed));
            Assert.Equal("已找到 2 个 Steam 国际区 compatibility environments。", Localization.Display("已找到 2 个 Steam 国际区环境。"));
            Assert.Equal("已找到 2 个 Proton。", Localization.Display("已找到 2 个 Proton。"));
        }
        finally { Localization.SetLanguage("zh-CN"); }
    }
    [Fact]
    public void EnglishPrefixKeepsSeparatorAndRuntimeValues()
    {
        try
        {
            Localization.SetLanguage("en");
            Assert.Equal("Launcher version: 0.11.4", Localization.Display("启动器版本：0.11.4"));
            Assert.Equal("Currently managed game region: Global", Localization.Display("当前管理的游戏区服：国际区"));
            Assert.Equal("Sync complete. Previous target data backup: /tmp/example", Localization.Display("同步完成。原目标数据备份：/tmp/example"));
        }
        finally { Localization.SetLanguage("zh-CN"); }
    }
}
