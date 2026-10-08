using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace XIVLauncher.Linux;

// Region-scoped drafts share one editor; credentials commit on Save.
public sealed class AccountManagerPanel : UserControl
{
    private readonly string root;
    private readonly Action<bool> saved;
    private readonly Action<bool> busyChanged;
    private List<CredentialDraft> drafts = [];
    private CredentialDraft? current;
    private string activeRegion = "ffxiv_cn";
    private readonly Dictionary<string, CredentialDraft> selections = new();
    private IEnumerable<CredentialDraft> VisibleDrafts => drafts.Where(x => x.Region == activeRegion);
    private readonly ComboBox accounts = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock region = new();
    private readonly TextBox name = new();
    private readonly TextBox password = new() { PasswordChar = '●' };
    private readonly TextBox secret = new() { PasswordChar = '●', Watermark = "可选的 Base32 密钥；清空后保存即删除" };
    private readonly StackPanel secretFields = new() { Spacing = 8 };
    private readonly StackPanel editor = new() { Spacing = 10 };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel panel = new() { Spacing = 10 };
    private readonly Button save;
    private bool restoring, loading;
    private long loadGeneration;
    public bool Busy { get; private set; }
    public bool HasChanges => drafts.Any(x => x.Dirty);
    public AccountManagerPanel(string root, Action<bool> saved, Action<bool> busyChanged)
    {
        this.root = root; this.saved = saved; this.busyChanged = busyChanged; Content = panel;
        panel.Children.Add(new TextBlock { Text = "编辑、删除均在点击“保存”后写入；其他游戏区服的草稿单独保留。", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(region); panel.Children.Add(accounts);
        var actions = new WrapPanel(); actions.Children.Add(MakeButton("添加账号", Add));
        actions.Children.Add(MakeButton("删除账号", () =>
        {
            if (current == null) return;
            if (!current.Existing) { drafts.Remove(current); current = VisibleDrafts.FirstOrDefault(); RefreshList(); Show(current); return; }
            current.Delete = !current.Delete; current.Dirty = true;
            feedback.Text = current.Delete ? "已标记删除；保存后删除账号及关联凭据。" : "已取消删除标记。"; RefreshList();
        }));
        actions.Children.Add(MakeButton("放弃修改", () => { ReloadRegion(true); }));
        save = MakeButton("保存", () => _ = SaveAsync()); actions.Children.Add(save); panel.Children.Add(actions);
        AddField(editor,"登录名",name);
        editor.Children.Add(new TextBlock { Text = "更换已保存账号的登录名或游戏区服，请新增账号后删除旧记录。", TextWrapping = TextWrapping.Wrap });
        AddField(secretFields,"密码",password); AddField(secretFields,"2FA 密钥",secret); editor.Children.Add(secretFields);
        panel.Children.Add(editor); panel.Children.Add(feedback);
        panel.Children.Add(MakeButton("本地 2FA 验证码生成器", () => _ = OpenOtpToolAsync()));
        accounts.SelectionChanged += (_, _) => { if (!restoring) Show(accounts.SelectedItem as CredentialDraft); };
        foreach (var input in new[] { name, password, secret })
            input.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Edited(); };
    }

    private async Task OpenOtpToolAsync()
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            await new LocalOtpWindow(secretFields.IsVisible ? secret.Text ?? "" : "").ShowDialog(owner);
    }
    private static void AddField(StackPanel panel, string title, Control control) { panel.Children.Add(new TextBlock { Text = title }); panel.Children.Add(control); }
    private static Button MakeButton(string title, Action action)
    { var button = new Button { Content = title, Margin = new Thickness(0,0,8,4) }; button.Click += (_, _) => action(); return button; }
    public void RefreshCurrentRegion() => SwitchRegion(LinuxSettings.Load(root).SelectedRegion);
    public void SwitchRegion(string selectedRegion)
    {
        if (Busy) return;
        if (!LinuxSettings.Regions.Any(x => x.Id == selectedRegion)) throw new ArgumentException("Unknown game region.");
        if (current != null) selections[activeRegion] = current;
        activeRegion = selectedRegion; ReloadRegion(false);
    }
    public void ClearDraftSecrets()
    {
        loadGeneration++;
        foreach (var draft in drafts) { draft.Password = ""; draft.Secret = ""; }
        restoring = true; password.Text = ""; secret.Text = ""; restoring = false;
    }
    private void ReloadRegion(bool discard)
    {
        var previous = selections.GetValueOrDefault(activeRegion);
        var retained = discard ? new List<CredentialDraft>() : VisibleDrafts.Where(x => x.Dirty).ToList();
        foreach (var draft in VisibleDrafts.Where(x => !retained.Contains(x))) { draft.Password = ""; draft.Secret = ""; }
        drafts.RemoveAll(x => x.Region == activeRegion); drafts.AddRange(retained);
        drafts.AddRange(CredentialDraftStore.Load(root).Where(x => x.Region == activeRegion && !retained.Any(d => d.Existing && d.Name == x.Name)));
        var selected = LinuxSettings.Load(root).RegionProfiles[activeRegion].Account;
        current = VisibleDrafts.FirstOrDefault(x => ReferenceEquals(x,previous)) ?? VisibleDrafts.FirstOrDefault(x => x.Name == previous?.Name)
            ?? VisibleDrafts.FirstOrDefault(x => x.Name == selected) ?? VisibleDrafts.FirstOrDefault();
        if (current == null) { current = new() { Region = activeRegion }; drafts.Add(current); }
        RefreshList(); Show(current);
        feedback.Text = retained.Count > 0 ? "当前游戏区服有未保存的修改。" : current.Existing ? "" : "可直接填写并保存当前游戏区服的新账号。";
    }
    public void GuideAccount(string selectedRegion, string accountName)
    {
        SwitchRegion(selectedRegion);
        current = VisibleDrafts.FirstOrDefault(x => x.Name == accountName);
        if (current == null)
        {
            current = new CredentialDraft { Region = activeRegion, Name = accountName, Dirty = true };
            drafts.Add(current);
        }
        RefreshList(); Show(current);
        feedback.Text = "这是首次登录此账户，请填写并保存凭据，然后手动回到游戏页选择自己的登录名并登录。";
        password.Focus();
    }
    private void Add()
    {
        current = VisibleDrafts.FirstOrDefault(x => !x.Existing && !x.Dirty && x.Name.Length == 0);
        if (current == null) { current = new() { Region = activeRegion }; drafts.Add(current); }
        RefreshList(); Show(current); name.Focus();
    }
    private void RefreshList()
    { restoring = true; accounts.ItemsSource = VisibleDrafts.ToArray(); accounts.SelectedItem = current; restoring = false; }
    private void Show(CredentialDraft? draft)
    {
        if (draft != null && draft.Region != activeRegion) return;
        var generation = ++loadGeneration; loading = false; save.IsEnabled = true;
        current = draft; if (draft != null) selections[activeRegion] = draft;
        restoring = true; editor.IsEnabled = draft != null;
        region.Text = Localization.Display("游戏区服：") + Localization.Display(LinuxSettings.Regions.Single(x => x.Id == activeRegion).Name);
        name.Text = draft?.Name ?? ""; name.IsReadOnly = draft?.Existing == true;
        password.Text = draft?.Password ?? ""; secret.Text = draft?.Secret ?? "";
        secretFields.IsVisible = activeRegion != "ffxiv_cn";
        restoring = false;
        if (draft is { Existing: true, CredentialsLoaded: false } && draft.Region != "ffxiv_cn")
        { loading = true; editor.IsEnabled = false; save.IsEnabled = false; _ = LoadSecretsAsync(draft,generation); }
    }
    private async Task LoadSecretsAsync(CredentialDraft draft, long generation)
    {
        try
        {
            var settings = LinuxSettings.Load(root); settings.SelectedRegion = draft.Region;
            var loadedPassword = await RegionCredentials.ReadAsync(settings,draft.Name,"password",default) ?? "";
            var loadedSecret = await RegionCredentials.ReadAsync(settings,draft.Name,"otp",default) ?? "";
            if (generation != loadGeneration) return;
            draft.Password = loadedPassword; draft.Secret = loadedSecret;
            draft.HadPassword = loadedPassword.Length != 0; draft.HadSecret = loadedSecret.Length != 0; draft.CredentialsLoaded = true;
            restoring = true; password.Text = loadedPassword; secret.Text = loadedSecret; restoring = false;
            editor.IsEnabled = true;
        }
        catch (Exception ex)
        {
            if (generation == loadGeneration)
            {
                feedback.Text = "无法读取此账号的凭据，请检查文件权限或日志后重新选择。";
                // Failed reads must not lock the user out of replacing credentials.
                editor.IsEnabled = true;
            }
            new SessionDiagnostics(root).Write(DiagnosticLog.Format(ex),draft.Region,"DETAIL");
        }
        finally { if (generation == loadGeneration) { loading = false; save.IsEnabled = true; } }
    }
    private void Edited()
    {
        if (restoring || current == null || loading) return;
        current.Name = name.Text ?? ""; current.Password = password.Text ?? ""; current.Secret = secret.Text ?? "";
        current.ClearPassword = current.HadPassword && current.Password.Length == 0;
        current.ClearOtp = current.HadSecret && string.IsNullOrWhiteSpace(current.Secret);
        current.Dirty = true; feedback.Text = "有未保存的修改。"; RefreshList();
    }
    private async Task SaveAsync()
    {
        if (Busy || loading) return;
        if (!VisibleDrafts.Any(x => x.Dirty)) { feedback.Text = "没有待保存的修改。"; return; }
        Busy = true; panel.IsEnabled = false; busyChanged(true);
        try
        {
            await CredentialDraftStore.SaveAsync(root,VisibleDrafts.ToArray(),default);
            ReloadRegion(true); feedback.Text = "修改已保存。"; saved(true);
        }
        catch (Exception ex)
        {
            feedback.Text = ex is CredentialValidationException validation ? validation.UserMessage : "保存未完成，请查看日志；已成功写入的部分会保留。";
            new SessionDiagnostics(root).Write(DiagnosticLog.Format(ex),"shared","DETAIL"); saved(false);
        }
        finally { Busy = false; panel.IsEnabled = true; busyChanged(false); }
    }
}
