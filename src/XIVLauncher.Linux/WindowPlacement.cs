namespace XIVLauncher.Linux;

// Shared launcher preference, independent of game-region settings and credentials.
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized)
{
    public bool IsValid => double.IsFinite(Width) && double.IsFinite(Height) && Width >= 640 && Height >= 650 && Width <= 20000 && Height <= 20000;
}
