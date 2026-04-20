using SubwaySurfer.Entities;

namespace SubwaySurfer.Engine;

/// <summary>
/// Pure game logic — zero WPF / UI references.
/// GameEngine delegates all update logic here so tests can run headless.
/// </summary>
public class GameLogic
{
    // ── Constants ────────────────────────────────────────
    public const double SpeedInit  = 500;
    public const double SpeedMax   = 1500;
    public const double SpeedAcc   = 8;
    public const double TrainKillZ = -30;
    public const double SafeZ      = 500;

    /// Z threshold at which the player visually overlaps an obstacle.
    /// GameEngine sets this from camera maths; tests use the default below.
    public double HitZ { get; set; } = 80.0;

    // ── State ────────────────────────────────────────────
    public Phase  Phase  { get; private set; } = Phase.Menu;
    public int    Score  { get; private set; }
    public double Speed  { get; private set; }
    public double Scroll { get; private set; }

    public Player      Player { get; } = new();
    public List<Train> Trains { get; } = new();
    public List<Coin>  Coins  { get; } = new();

    // Fired when the player dies — GameEngine subscribes to show Game Over UI
    public event Action? GameOverTriggered;

    private double _trainTimer;
    private double _coinTimer;
    private readonly Random _rng = new();

    // ── Public API ───────────────────────────────────────

    /// <summary>Force a specific phase (useful in tests).</summary>
    public void SetPhase(Phase p) => Phase = p;

    public void StartGame()
    {
        Phase       = Phase.Playing;
        Score       = 0;
        Speed       = SpeedInit;
        Scroll      = 0;
        _trainTimer = 1.0;
        _coinTimer  = 0.5;
        Trains.Clear();
        Coins.Clear();
        Player.Reset();
    }

    public void GoToMenu()
    {
        Phase = Phase.Menu;
        Player.Reset();
        Trains.Clear();
        Coins.Clear();
    }

    /// <summary>
    /// Advance the simulation by <paramref name="dt"/> seconds.
    /// Call this every frame (from GameEngine.Tick or directly in tests).
    /// </summary>
    public void Update(double dt)
    {
        if (Phase != Phase.Playing) return;

        // Speed & score
        Speed  = Math.Min(SpeedMax, Speed + SpeedAcc * dt);
        Score += (int)(Speed * dt * 0.1);
        Scroll += Speed * dt;

        // Player
        Player.Update(dt, Speed);

        // Spawn trains
        _trainTimer -= dt;
        if (_trainTimer <= 0)
        {
            SpawnTrain();
            double ratio = SpeedInit / Speed;
            _trainTimer = 0.50 + _rng.NextDouble() * 1.10 * ratio;
        }

        // Spawn coins
        _coinTimer -= dt;
        if (_coinTimer <= 0)
        {
            Coins.Add(Coin.Spawn(_rng.Next(3) - 1));
            _coinTimer = 0.30 + _rng.NextDouble() * 0.60;
        }

        // Move objects
        foreach (var t in Trains) t.Z -= Speed * dt;
        foreach (var c in Coins)  { c.Z -= Speed * dt; c.Bob += dt * 5; }

        // Train collision
        bool airborne = Player.IsJumping && Player.JumpY > Player.JumpSafe;
        foreach (var t in Trains)
        {
            if (t.Lane == Player.Lane && t.Z < HitZ && t.Z > 0 && !airborne)
            {
                Phase = Phase.GameOver;
                GameOverTriggered?.Invoke();
                return;
            }
        }

        // Coin collection
        foreach (var c in Coins)
        {
            if (!c.Dead && c.Lane == Player.Lane && c.Z < HitZ && c.Z > 0)
            {
                c.Dead  = true;
                Score  += 10;
            }
        }

        // Cleanup
        Trains.RemoveAll(t => t.Z < TrainKillZ);
        Coins.RemoveAll(c  => c.Z < TrainKillZ || c.Dead);
    }

    // ── Internal helpers ─────────────────────────────────

    private void SpawnTrain()
    {
        double diff       = Math.Min(1, (Speed - SpeedInit) / (SpeedMax - SpeedInit));
        double pairChance = 0.12 + diff * 0.28;

        if (_rng.NextDouble() < pairChance)
        {
            int safe = _rng.Next(3) - 1;
            if (!LaneBlocked(safe))
            {
                for (int l = -1; l <= 1; l++)
                    if (l != safe) Trains.Add(Train.Spawn(l));
                return;
            }
        }

        int lane = _rng.Next(3) - 1;
        if (!LaneBlocked(lane))
            Trains.Add(Train.Spawn(lane));
    }

    private bool LaneBlocked(int lane) =>
        Trains.Exists(t => t.Lane == lane && Math.Abs(t.Z - 2500) < SafeZ);
}
