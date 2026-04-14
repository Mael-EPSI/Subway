namespace SubwaySurfer.Game;

public enum GamePhase { Menu, Playing, GameOver }

public sealed class GameState
{
    public GamePhase Phase { get; set; } = GamePhase.Menu;
    public int Score { get; set; }
    public int HighScore { get; set; }
    public double Speed { get; set; } = GameConfig.SpeedInitial;
    public double Scroll { get; set; }
    public double Time { get; set; }

    public PlayerState Player { get; } = new();
    public List<Train> Trains { get; } = [];
    public List<Coin> Coins { get; } = [];
    public List<Particle> Particles { get; } = [];

    public double TrainTimer { get; set; } = 1.0;
    public double CoinTimer { get; set; } = 0.5;

    public double ShakeTime { get; set; }
    public double FlashAlpha { get; set; }

    // Sounds queued this frame — client will play them
    public List<string> Sounds { get; } = [];
}
