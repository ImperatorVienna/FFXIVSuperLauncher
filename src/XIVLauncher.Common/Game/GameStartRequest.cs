namespace XIVLauncher.Common.Game;

public sealed record GameStartRequest
(
    string                      ExePath,
    string                      WorkingDirectory,
    string                      Arguments
);
