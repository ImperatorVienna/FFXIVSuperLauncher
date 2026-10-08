using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using XIVLauncher.DCTravel;
namespace XIVLauncher.Linux.ChinaTravel;

public sealed class ChinaTravelWindow : Window
{
    private sealed record Choice<T>(T Value, string Label) { public override string ToString() => Label; }
    private readonly DCTravelClient client;
    private readonly Action ensureStopped;
    private readonly Action<string> setArea;
    private readonly Action<string, Exception?> log;
    private readonly StackPanel form = new() { Spacing = 9 };
    private readonly TextBlock historyPage = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox source = new(), character = new(), target = new(), destination = new(), orders = new(), returnArea = new(), returnGroup = new();
    private readonly CheckBox autoLaunch = new() { Content = "完成后启动游戏", IsChecked = false };
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? action;
    private List<DCTravelArea> sourceAreas = [];
    private int page = 1, pages = 1;
    private bool loading, submitted;
    public ChinaTravelWindow(DCTravelClient client, Action ensureStopped, Action<string> setArea, Action<string, Exception?> log)
    {
        this.client = client; this.ensureStopped = ensureStopped; this.setArea = setArea; this.log = log;
        Title = "超域传送"; Width = 700; Height = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        void Field(string name, Control input) { form.Children.Add(new TextBlock { Text = name }); input.HorizontalAlignment = HorizontalAlignment.Stretch; form.Children.Add(input); }
        Field("来源大区", source); Field("角色", character); Field("目标大区", target); Field("目标服务器", destination);
        form.Children.Add(autoLaunch);
        var transferActions = new WrapPanel { Orientation = Orientation.Horizontal };
        transferActions.Children.Add(Button("提交所选传送", async () => await TransferAsync()));
        var returnButton = Button("返回原区", async () => await ReturnAsync()); returnButton.Margin = new Thickness(8, 0, 0, 0);
        transferActions.Children.Add(returnButton); form.Children.Add(transferActions);
        form.Children.Add(status);
        form.Children.Add(new Separator()); Field("可返回的已完成传送记录", orders);
        var history = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        history.Children.Add(Button("刷新历史", () => RunAsync(ct => LoadOrdersAsync(ct))));
        history.Children.Add(Button("上一页", () => RunAsync(async ct => { page = Math.Max(1, page - 1); await LoadOrdersAsync(ct); })));
        history.Children.Add(Button("下一页", () => RunAsync(async ct => { page = Math.Min(pages, page + 1); await LoadOrdersAsync(ct); })));
        form.Children.Add(history); form.Children.Add(historyPage); Field("返回前角色当前所在大区", returnArea); Field("返回前角色当前所在服务器", returnGroup);
        var bottom = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        bottom.Children.Add(Button("停止等待", () => { action?.Cancel(); return Task.CompletedTask; }));
        bottom.Children.Add(new TextBlock { Text = "停止等待或关闭窗口不会撤销已提交的服务端传送。可重新打开并刷新记录确认结果。", TextWrapping = TextWrapping.Wrap });
        var pageContent = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        pageContent.Children.Add(form); pageContent.Children.Add(bottom);
        Content = new ScrollViewer { Content = pageContent };
        source.SelectionChanged += async (_, _) => { if (!loading && !Busy) await RunAsync(LoadCharactersAsync); };
        character.SelectionChanged += async (_, _) => { if (!loading && !Busy) await RunAsync(LoadTargetsAsync); };
        target.SelectionChanged += (_, _) => FillDestinations();
        returnArea.SelectionChanged += (_, _) => FillReturnGroups();
        orders.SelectionChanged += (_, _) =>
        {
            if (Selected<DCTravelMigrationOrder>(orders) is not { } order) return;
            returnArea.SelectedItem = Items<DCTravelArea>(returnArea).FirstOrDefault(x => x.Value.AreaName == order.TargetAreaName);
            returnGroup.SelectedItem = Items<DCTravelGroup>(returnGroup).FirstOrDefault(x => x.Value.GroupName == order.TargetGroupName);
        };
        Opened += async (_, _) => await RunAsync(async ct =>
        {
            sourceAreas = await client.QueryGroupListTravelSource().WaitAsync(ct);
            loading = true; Fill(source, sourceAreas, x => x.AreaName); Fill(returnArea, sourceAreas, x => x.AreaName); loading = false;
            await LoadCharactersAsync(ct); await LoadOrdersAsync(ct);
        });
        Closed += (_, _) => { lifetime.Cancel(); action?.Cancel(); client.EndSession(); };
    }
    private bool Busy => action != null;
    private static T? Selected<T>(ComboBox box) where T : class => (box.SelectedItem as Choice<T>)?.Value;
    private static IEnumerable<Choice<T>> Items<T>(ComboBox box) => box.ItemsSource as IEnumerable<Choice<T>> ?? [];
    private static void Fill<T>(ComboBox box, IEnumerable<T> values, Func<T, string> label)
    { box.ItemsSource = values.Select(x => new Choice<T>(x, label(x))).ToArray(); box.SelectedIndex = box.ItemCount > 0 ? 0 : -1; }
    private static Button Button(string title, Func<Task> action)
    { var b = new Button { Content = title }; b.Click += async (_, _) => await action(); return b; }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (Busy || lifetime.IsCancellationRequested) return;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); action = cts; form.IsEnabled = false;
        client.BeginSession(); using var cancellation = cts.Token.Register(client.EndSession);
        try { await work(cts.Token); }
        catch (OperationCanceledException) { status.Text = submitted ? "已停止等待，服务端操作可能仍在继续；请刷新历史确认。" : "操作已取消。"; log("DC travel operation cancelled; submitted orders may still be processing.", null); }
        catch (Exception ex) { status.Text = "操作失败，请检查日志后重试；如已提交，请先刷新历史确认结果。"; log(UserLogMessage.Failure(ex), ex); }
        finally { action = null; loading = false; form.IsEnabled = true; }
    }
    private async Task LoadCharactersAsync(CancellationToken ct)
    {
        loading = true; character.ItemsSource = null; target.ItemsSource = null; destination.ItemsSource = null;
        if (Selected<DCTravelArea>(source) is not { } area) { loading = false; return; }
        var all = new List<DCTravelCharacter>();
        foreach (var group in area.GroupList)
        {
            var characters = await client.QueryRoleList(area.AreaID, group.GroupID).WaitAsync(ct);
            foreach (var c in characters) { c.ServerName = group.GroupName; all.Add(c); }
        }
        Fill(character, all, c => c.Name + " · " + c.ServerName); loading = false; await LoadTargetsAsync(ct);
    }
    private async Task LoadTargetsAsync(CancellationToken ct)
    {
        target.ItemsSource = null; destination.ItemsSource = null;
        if (Selected<DCTravelCharacter>(character) is not { } role) return;
        var choices = await client.QueryGroupListTravelTarget(role.AreaID, role.GroupID).WaitAsync(ct);
        Fill(target, choices, x => x.AreaName); FillDestinations();
    }
    private void FillDestinations() => Fill(destination, Selected<DCTravelArea>(target)?.GroupList ?? [], g => g.GroupName);
    private void FillReturnGroups() => Fill(returnGroup, Selected<DCTravelArea>(returnArea)?.GroupList ?? [], g => g.GroupName);
    private async Task LoadOrdersAsync(CancellationToken ct)
    {
        if (!await client.InitOrderPage().WaitAsync(ct)) throw new IOException("DC travel history is unavailable.");
        var result = await client.QueryMigrationOrders(page).WaitAsync(ct); pages = Math.Max(1, result.TotalPageNum);
        Fill(orders, result.Orders, x => $"{x.RoleName} · {x.SourceAreaName}/{x.SourceGroupName} → {x.TargetAreaName}/{x.TargetGroupName} · {x.CreateTime}");
        historyPage.Text = $"历史记录：第 {page}/{pages} 页。";
    }
    private async Task<bool> ConfirmAsync(string text)
    {
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        var dialog = new Window { Title = "确认超域操作", Width = 440, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        panel.Children.Add(Button("确认提交", () => { dialog.Close(true); return Task.CompletedTask; }));
        panel.Children.Add(Button("取消", () => { dialog.Close(false); return Task.CompletedTask; }));
        return await dialog.ShowDialog<bool>(this);
    }
    private async Task TransferAsync()
    {
        if (Busy || Selected<DCTravelCharacter>(character) is not { } role || Selected<DCTravelGroup>(destination) is not { } to || Selected<DCTravelArea>(source) is not { } from) return;
        var group = from.GroupList.FirstOrDefault(g => g.AreaID == role.AreaID && g.GroupID == role.GroupID);
        if (group == null) { status.Text = "无法确定角色来源服务器，请重新加载。"; return; }
        if (!await ConfirmAsync($"将 {role.Name} 从 {from.AreaName}/{group.GroupName} 传送至 {to.AreaName}/{to.GroupName}？")) return;
        await RunAsync(async ct => { ensureStopped(); submitted = true; var id = await client.TravelOrder(to, group, role).WaitAsync(ct); await CompleteAsync(id, to.AreaName, ct); });
    }
    private async Task ReturnAsync()
    {
        if (Busy || Selected<DCTravelMigrationOrder>(orders) is not { } order || Selected<DCTravelGroup>(returnGroup) is not { } group) return;
        if (!await ConfirmAsync($"将 {order.RoleName} 从当前选择的 {group.AreaName}/{group.GroupName} 返回 {order.SourceAreaName}/{order.SourceGroupName}？")) return;
        await RunAsync(async ct => { ensureStopped(); submitted = true; var id = await client.TravelBack(order.OrderID, group.GroupID, group.GroupCode, group.GroupName).WaitAsync(ct); await CompleteAsync(id, order.SourceAreaName, ct); });
    }
    private async Task CompleteAsync(string id, string areaName, CancellationToken ct)
    {
        await TravelOrderMonitor.WaitAsync(client.QueryOrderStatus, client.MigrationConfirmOrder, id,
            text => status.Text = text, ct, onSuccess: () => { if (!string.IsNullOrWhiteSpace(areaName)) setArea(areaName); });
        submitted = false; status.Text = "超域操作完成。"; log("DC travel completed; selected data center updated.", null);
        if (autoLaunch.IsChecked == true && !string.IsNullOrWhiteSpace(areaName)) Close(true);
        else await LoadOrdersAsync(ct);
    }
}
