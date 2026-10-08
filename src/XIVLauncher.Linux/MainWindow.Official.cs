using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly StackPanel officialPanel = new() { Spacing = 8 };
    private CancellationTokenSource? newsCancellation;
    private static readonly HttpClient NewsClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private async Task RefreshOfficialAsync()
    {
        newsCancellation?.Cancel();
        using var request = new CancellationTokenSource(); newsCancellation = request;
        var region = settings.SelectedRegion;
        officialPanel.Children.Clear();
        var links = OfficialContent.LinksFor(region, settings.Current.CdKeyVersion, settings.Current.EuShopLanguage);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        void AddLink(string title, string url)
        {
            var button = Button(Localization.T(title), () => OpenOfficialUrl(url));
            button.Margin = new Avalonia.Thickness(0, 0, 8, 4); buttons.Children.Add(button);
        }
        AddLink("游戏官网", links.Home); AddLink("官方商城", links.Shop);
        if (links.Subscription != null) AddLink("游戏时长充值", links.Subscription);
        if (links.MogStation != null) AddLink("Mog Station", links.MogStation);
        officialPanel.Children.Add(buttons);
        var content = new StackPanel { Spacing = 6 };
        officialPanel.Children.Add(new Expander { Header = Localization.T("官方活动与更新公告"), IsExpanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = new ScrollViewer { Content = content, MaxHeight = 500 } });
        content.Children.Add(new TextBlock { Text = Localization.T("正在获取官方资讯…") });
        try
        {
            var news = await OfficialContent.LoadAsync(NewsClient, region, request.Token, settings.Current.CdKeyVersion, settings.Current.EuShopLanguage);
            if (request.IsCancellationRequested || settings.SelectedRegion != region) return;
            content.Children.Clear();
            void AddSection(string title, IReadOnlyList<OfficialArticle> articles)
            {
                content.Children.Add(new TextBlock { Text = Localization.T(title), FontWeight = FontWeight.SemiBold });
                foreach (var item in articles)
                {
                    var button = Button(item.Title, () => OpenOfficialUrl(item.Url));
                    button.Content = new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap };
                    button.HorizontalAlignment = HorizontalAlignment.Stretch; content.Children.Add(button);
                }
                if (articles.Count == 0) content.Children.Add(new TextBlock { Text = Localization.T("暂无资讯，请前往官网查看。") });
            }
            AddSection("官方活动", news.Activities); AddSection("更新公告", news.Notices);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (settings.SelectedRegion == region && !request.IsCancellationRequested)
            { content.Children.Clear(); content.Children.Add(new TextBlock { Text = Localization.T("暂无资讯，请前往官网查看。"), TextWrapping = TextWrapping.Wrap }); AppendLog("Official news unavailable; game sign-in is unaffected.", "WARN", region); AppendLog(DiagnosticLog.Format(ex), "DETAIL", region); }
        }
        finally { if (newsCancellation == request) newsCancellation = null; }
    }
    private void OpenOfficialUrl(string url) => OfficialBrowser.Open(url, settings.Root);
}
