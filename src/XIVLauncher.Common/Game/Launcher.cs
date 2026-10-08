using System.Text.RegularExpressions;
using Serilog;
using XIVLauncher.Common.Constant;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game;

public partial class Launcher
{
    public GameStartRequest CreateGameStartRequest
    (
        string        sessionID,
        string        sndaID,
        int           dcTravelPort,
        string        areaID,
        string        lobbyHost,
        string        gmHost,
        string        dbHost,
        string        areasInfo,
        string        additionalArguments,
        DirectoryInfo gamePath
    )
    {
        Log.Information("[Launcher] 启动游戏 (参数: {AdditionalArguments})", additionalArguments);

        var exePath     = Path.Combine(gamePath.FullName, "game", "ffxiv_dx11.exe");

        var argumentBuilder = new ArgumentBuilder()
                              .Append("-AppID",                     SdoInfos.APP_ID)
                              .Append("-AreaID",                    areaID)
                              .Append("Dev.LobbyHost01",            lobbyHost)
                              .Append("Dev.LobbyPort01",            "54994")
                              .Append("Dev.GMServerHost",           gmHost)
                              .Append("Dev.SaveDataBankHost",       dbHost)
                              .Append("resetConfig",                "0")
                              .Append("DEV.MaxEntitledExpansionID", "1")
                              .Append("DEV.TestSID",                sessionID)
                              .Append("XL.SndaId",                  sndaID)
                              .Append("XL.LobbyHosts",              areasInfo)
                              .Append("XL.DcTraveler",              $"{dcTravelPort}");

        if (!string.IsNullOrEmpty(additionalArguments))
        {
            foreach (Match match in AdditionalArgumentsRegex().Matches(additionalArguments))
                argumentBuilder.Append(match.Groups["key"].Value, match.Groups["value"].Value);
        }

        if (!File.Exists(exePath))
            throw new BinaryNotPresentException(exePath);

        var workingDir = Path.Combine(gamePath.FullName, "game");
        var arguments = argumentBuilder.Build();

        return new GameStartRequest(exePath, workingDir, arguments);
    }

    [GeneratedRegex(@"\s*(?<key>[^=]+)\s*=\s*(?<value>[^\s]+)\s*", RegexOptions.Compiled)]
    private static partial Regex AdditionalArgumentsRegex();


}
