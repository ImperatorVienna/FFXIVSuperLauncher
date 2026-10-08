namespace XIVLauncher.Linux;
public sealed class CredentialDraft
{
    public string Region { get; set; } = "ffxiv_cn";
    public string Name { get; set; } = "";
    public bool Existing { get; set; }
    public string Password { get; set; } = "";
    public string Secret { get; set; } = "";
    public bool RememberChina { get; set; } = true;
    public bool Delete { get; set; }
    public bool CredentialsLoaded { get; set; }
    public bool HadPassword { get; set; }
    public bool HadSecret { get; set; }
    public bool ClearPassword { get; set; }
    public bool ClearOtp { get; set; }
    public bool Dirty { get; set; }
    public override string ToString() => $"{Localization.Display(LinuxSettings.Regions.Single(x => x.Id == Region).Name)} · {(Name.Length == 0 ? Localization.Display("新账号") : Name)}{(Delete ? Localization.Display("（待删除）") : Dirty ? Localization.Display("（未保存）") : "")}";
}
public static class CredentialDraftStore
{
    public static IReadOnlyList<CredentialDraft> Load(string root)
    {
        var settings = LinuxSettings.Load(root);
        return LinuxSettings.Regions.SelectMany(region => settings.RegionProfiles[region.Id].Accounts.OrderBy(x => x.Key).Select(pair => new CredentialDraft
        {
            Region = region.Id, Name = pair.Key, Existing = true,
            RememberChina = pair.Value.RememberPassword
        })).ToArray();
    }
    public static async Task SaveAsync(string root, IReadOnlyList<CredentialDraft> drafts, CancellationToken token)
    {
        var dirty = drafts.Where(x => x.Dirty).ToArray();
        var settings = LinuxSettings.Load(root); var selectedRegion = settings.SelectedRegion;
        var identities = new HashSet<(string,string)>();
        // Validate the complete batch before any credential writes or deletions.
        foreach (var draft in dirty)
        {
            draft.Name = draft.Name.Trim();
            if (!LinuxSettings.Regions.Any(x => x.Id == draft.Region) || draft.Name.Length == 0)
                throw new CredentialValidationException("A game region and account name are required.", "请为每个待保存账号填写游戏区服和登录名。");
            if (!identities.Add((draft.Region,draft.Name))) throw new CredentialValidationException("Duplicate account draft.", "同一游戏区服存在重复登录名，请合并修改后再保存。");
            var exists = settings.RegionProfiles[draft.Region].Accounts.ContainsKey(draft.Name);
            if (!draft.Existing && exists) throw new CredentialValidationException("The account already exists.", "此游戏区服已存在该登录名，请编辑原记录。");
            if (draft.Existing && !exists) throw new CredentialValidationException("The saved account no longer exists.", "账号记录已变化，请放弃修改后重新载入。");
            if (draft.Delete) continue;
            if (draft.Region == "ffxiv_cn")
            {
                if (draft.Password.Length != 0 || draft.Secret.Length != 0) throw new InvalidOperationException("ffxiv_cn does not accept static credentials.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(draft.Secret))
            {
                if (draft.Password.Length == 0) throw new CredentialValidationException("A new 2FA secret requires a password.", "填写新的 2FA 密钥时，请同时填写账号密码。");
                try { _ = OneTimePassword.Decode(draft.Secret); }
                catch (ArgumentException) { throw new CredentialValidationException("Invalid 2FA secret.", "请输入有效 Base32 密钥，不是六位验证码。"); }
            }

        }
        foreach (var draft in dirty)
        {
            settings.SelectedRegion = draft.Region;
            var profile = settings.Current; var previousAccount = profile.Account;
            if (draft.Delete)
            {
                await AccountProfiles.DeleteAsync(settings,draft.Name,token,persist:false);
                settings.SelectedRegion = selectedRegion; settings.Save(); draft.Dirty = false;
                continue;
            }
            AccountProfiles.Select(profile,draft.Name);
            profile.RememberPassword = true;
            try
            {
                if (draft.ClearPassword) await RegionCredentials.RemoveAsync(settings,draft.Name,"password",token);
                if (draft.ClearOtp) await RegionCredentials.RemoveAsync(settings,draft.Name,"otp",token);
                if (draft.Password.Length != 0) await RegionCredentials.StoreValueAsync(root, draft.Region, draft.Name, "password", draft.Password, token);
                if (!string.IsNullOrWhiteSpace(draft.Secret)) await RegionCredentials.StoreValueAsync(root, draft.Region, draft.Name, "otp", draft.Secret, token);
            }
            finally
            {
                // Keep successful storage writes discoverable even if a later file write fails.
                AccountProfiles.Capture(profile);
                if (previousAccount != draft.Name) AccountProfiles.Select(profile,previousAccount);
                settings.SelectedRegion = selectedRegion; settings.Save(); draft.Existing = true;
            }
            draft.Password = ""; draft.Secret = ""; draft.ClearOtp = false; draft.ClearPassword = false; draft.Dirty = false;
        }
        settings.SelectedRegion = selectedRegion; settings.Save();
    }
}
