namespace SubwaySurfer.Game;

public static class GameConfig
{
    public const int Width = 800;
    public const int Height = 600;
    public const double Fov = 250;
    public const double VpYRatio = 0.35;

    public const double LaneW = 110;
    public const double TrackHW = 180;

    // Player
    public const double LaneLerp = 12;
    public const double JumpDur = 0.50;
    public const double JumpH = 130;
    public const double JumpSafe = 30;

    // Trains
    public const double TrainSpawnZ = 2500;
    public const double TrainKillZ = -30;
    public const double TrainHitZ = 65;
    public const double TrainW = 86;
    public const double TrainH = 115;
    public const double TrainGapMin = 0.50;
    public const double TrainGapMax = 1.60;
    public const double TrainSafeZ = 500;

    // Coins
    public const double CoinSpawnZ = 2500;
    public const double CoinHitZ = 65;
    public const double CoinR = 14;
    public const int CoinValue = 10;
    public const double CoinGapMin = 0.30;
    public const double CoinGapMax = 0.90;
    public const double CoinFloatH = 40;

    // Speed
    public const double SpeedInitial = 700;
    public const double SpeedMax = 2000;
    public const double SpeedAccel = 8;

    // Rendering hints
    public const double MaxZ = 3000;
    public const double TieSpacing = 180;

    public static readonly string[] TrainColors =
        ["#cc2222", "#2255cc", "#22aa44", "#cc7722", "#8833bb", "#cc2288"];
}
