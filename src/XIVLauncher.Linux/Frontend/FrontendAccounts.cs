using System.Security.Cryptography;

namespace XIVLauncher.Linux.Frontend;

internal sealed record AccountIdentity(string Region, string Name);
internal sealed record AccountChange(string Region, string Name, bool Existing, string Password = "", string Secret = "", bool Delete = false, string? Revision = null);
internal sealed record AccountSummary(string Name, bool HasPassword, bool HasOtp, bool SteamAccount, bool FreeTrial, string AreaName);

// One editor and one store for all regions; only an explicit editor read returns saved secrets.
internal sealed class FrontendAccounts(string root)
{
    private readonly Dictionary<string, (string Region, string Name, string Fingerprint)> revisions = new();
    internal static void Validate(string region, string? name = null)
    {
        if (!LinuxSettings.Regions.Any(r => r.Id == region) || name != null && (string.IsNullOrWhiteSpace(name) || name.Length > 256))
            throw new ArgumentException("Invalid account identity.");
    }
    internal async Task<object> ListAsync(string region, CancellationToken token)
    {
        Validate(region);
        var settings = LinuxSettings.Load(root); var profile = settings.RegionProfiles[region];
        var accounts = new List<AccountSummary>();
        foreach (var name in profile.Accounts.Keys.Order())
        {
            var saved = profile.Accounts[name];
            accounts.Add(new(name, !string.IsNullOrEmpty(await RegionCredentials.ReadAsync(root,region,name,"password",token)),
                !string.IsNullOrWhiteSpace(await RegionCredentials.ReadAsync(root,region,name,"otp",token)), saved.SteamAccount, saved.FreeTrial, saved.AreaName));
        }
        return new { region, selectedAccount = profile.Account, enableDalamud = profile.EnableDalamud, accounts };
    }
    internal async Task<object> ReadAsync(AccountIdentity identity, CancellationToken token)
    {
        Validate(identity.Region, identity.Name);
        if (!LinuxSettings.Load(root).RegionProfiles[identity.Region].Accounts.ContainsKey(identity.Name))
            throw new ArgumentException("Unknown saved account.");
        var fingerprint=Fingerprint(identity.Region);
        var password = identity.Region == "ffxiv_cn" ? "" : await RegionCredentials.ReadAsync(root,identity.Region,identity.Name,"password",token) ?? "";
        var secret = identity.Region == "ffxiv_cn" ? "" : await RegionCredentials.ReadAsync(root,identity.Region,identity.Name,"otp",token) ?? "";
        if(fingerprint!=Fingerprint(identity.Region))throw new IOException("Credentials changed during read.");
        if (revisions.Count >= 64) revisions.Remove(revisions.Keys.First());
        var revision = Guid.NewGuid().ToString("N");
        revisions[revision] = (identity.Region,identity.Name,fingerprint);
        return new { region=identity.Region, name=identity.Name, password, secret, revision };
    }
    internal async Task<object> SaveAsync(AccountChange change, CancellationToken token)
    {
        Validate(change.Region,change.Name);
        if (change.Password == null || change.Secret == null || change.Password.Length > 4096 || change.Secret.Length > 4096)
            throw new ArgumentException("Invalid credential fields.");
        if (change.Existing && (change.Revision == null || !revisions.TryGetValue(change.Revision,out var original) ||
            original.Region != change.Region || original.Name != change.Name || original.Fingerprint != Fingerprint(change.Region)))
            throw new CredentialValidationException("Credential editor is stale.","账号记录已变化，请放弃修改后重新载入。");
        if (change.Delete && !change.Existing) throw new ArgumentException("Cannot delete an unsaved account.");
        await CredentialDraftStore.SaveAsync(root,[new CredentialDraft
        {
            Region=change.Region, Name=change.Name, Existing=change.Existing, Password=change.Password, Secret=change.Secret,
            ClearPassword=change.Existing && change.Password.Length==0, ClearOtp=change.Existing && string.IsNullOrWhiteSpace(change.Secret),
            Delete=change.Delete, Dirty=true
        }],token);
        if (change.Revision != null) revisions.Remove(change.Revision);
        return new { saved=true };
    }
    private string Fingerprint(string region)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var name in new[]{"settings.json","credentials.json"})
        {
            var file=Path.Combine(root,region,name);
            if(File.Exists(file)) hash.AppendData(File.ReadAllBytes(file));
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset()); // Kept in this process; never returned to the renderer.
    }
}
