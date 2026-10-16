using System.Text.Json;

namespace XIVLauncher.Linux.Platforms.Windows;

// The PID is pinned by the caller before inspecting its image and acknowledging it.
// A primary supervisor requires the current discovery token, not just an EXE name.
public sealed record WindowsSessionHandoff(int ProcessId, string? PrimarySession)
{
    public static WindowsSessionHandoff Parse(string text, int supervisor)
    {
        var fields = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is < 1 or > 2 || !int.TryParse(fields[0], out var pid) || pid <= 0 || pid == supervisor ||
            (fields.Length == 2 && !Guid.TryParseExact(fields[1], "D", out _)))
            throw new IOException("Invalid Windows session handoff.");
        return new(pid, fields.Length == 2 ? fields[1] : null);
    }

    public void Validate(string? image, string backendImage, string supervisorImage, string? discovery)
    {
        if (!string.Equals(image, PrimarySession == null ? backendImage : supervisorImage, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Unexpected session process.");
        if (PrimarySession == null) return;
        using var json = JsonDocument.Parse(discovery ?? throw new IOException("Primary session is unavailable."));
        var root = json.RootElement;
        if (!root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var value) || value != ProcessId ||
            !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != PrimarySession)
            throw new IOException("Primary session has changed.");
    }
}
