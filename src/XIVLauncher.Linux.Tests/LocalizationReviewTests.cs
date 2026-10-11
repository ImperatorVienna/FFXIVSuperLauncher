using Xunit;
namespace XIVLauncher.Linux.Tests;
[CollectionDefinition("Localization", DisableParallelization = true)]
public sealed class LocalizationCollection { }
[Collection("Localization")]
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
    public void VerificationAndCredentialMessagesRetainReviewedTranslations()
    {
        try
        {
            Localization.SetLanguage("en");
            foreach(var key in new[]{"正在执行官方网页验证…","填写新的 2FA 密钥时，请同时填写账号密码。","在系统浏览器中打开"})
                Assert.NotEqual(key,Localization.Display(key));
            Assert.Equal("日本語",Localization.Display("日本語"));
            Assert.Equal("synthetic-account",Localization.Display("synthetic-account"));
        }
        finally { Localization.SetLanguage("zh-CN"); }
    }
}
