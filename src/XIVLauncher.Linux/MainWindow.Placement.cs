using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private bool placementReady, automaticMinimize;
    private WindowPlacement? lastPlacement;
    private readonly DispatcherTimer placementTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private void ObserveWindowPlacement()
    {
        placementTimer.Tick += (_, _) => { placementTimer.Stop(); CaptureWindowPlacement(); };
        PropertyChanged += (_, e) =>
        {
            if (e.Property != WindowStateProperty && e.Property != ClientSizeProperty) return;
            if (WindowState != WindowState.Minimized) automaticMinimize = false;
            SchedulePlacementCapture();
        };
        PositionChanged += (_, _) => SchedulePlacementCapture();
    }
    private void SchedulePlacementCapture()
    {
        if (!placementReady) return;
        placementTimer.Stop(); placementTimer.Start();
    }
    private void RestoreWindowPlacement()
    {
        if (settings.WindowPlacement is { IsValid: true } saved)
        {
            lastPlacement = saved;
            var screen = Screens.All.FirstOrDefault(s => s.WorkingArea.Contains(new PixelPoint(saved.X + 40, saved.Y + 20))) ?? Screens.Primary;
            if (screen != null)
            {
                var area = screen.WorkingArea; var scale = screen.Scaling;
                Width = Math.Max(MinWidth, Math.Min(saved.Width, area.Width / scale));
                Height = Math.Max(MinHeight, Math.Min(saved.Height, area.Height / scale));
                Position = new PixelPoint(Math.Clamp(saved.X, area.X, Math.Max(area.X, area.Right - (int)(Width * scale))),
                    Math.Clamp(saved.Y, area.Y, Math.Max(area.Y, area.Bottom - (int)(Height * scale))));
            }
            else { Width = saved.Width; Height = saved.Height; }
            lastPlacement = new(Position.X, Position.Y, Width, Height, saved.Maximized);
            if (saved.Maximized) WindowState = WindowState.Maximized;
        }
        placementReady = true;
        CaptureWindowPlacement();
    }
    private void CaptureWindowPlacement()
    {
        if (!placementReady || automaticMinimize || WindowState == WindowState.Minimized) return;
        if (WindowState == WindowState.Normal)
            lastPlacement = new(Position.X, Position.Y, ClientSize.Width, ClientSize.Height, false);
        else if (WindowState == WindowState.Maximized && lastPlacement != null)
            lastPlacement = lastPlacement with { Maximized = true };
    }
    private void MinimizeAfterGameLaunch()
    {
        CaptureWindowPlacement();
        automaticMinimize = true;
        WindowState = WindowState.Minimized;
    }
    private void SaveWindowPlacement()
    {
        placementTimer.Stop();
        if (!settingsLoaded || lastPlacement is not { IsValid: true }) return;
        // Use the last visible state; Closed may arrive after native geometry is destroyed.
        settings.WindowPlacement = lastPlacement;
        try { settings.SaveGlobalPreferences(); }
        catch (Exception ex) { sessionDiagnostics.Write(DiagnosticLog.Format(ex), "shared", "ERROR"); }
    }
}
