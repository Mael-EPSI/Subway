namespace SubwaySurfer.Game;

public sealed class GameEngine
{
    private static readonly Random Rng = new();

    private readonly GameState _s = new();

    public GameState State => _s;

    // ─── Public input actions ───────────────────────
    public void ActionLeft()
    {
        if (_s.Phase == GamePhase.Playing && _s.Player.Lane > -1)
        {
            _s.Player.Lane--;
            _s.Sounds.Add("lane");
        }
    }

    public void ActionRight()
    {
        if (_s.Phase == GamePhase.Playing && _s.Player.Lane < 1)
        {
            _s.Player.Lane++;
            _s.Sounds.Add("lane");
        }
    }

    public void ActionJump()
    {
        if (_s.Phase == GamePhase.Playing && !_s.Player.Jumping)
        {
            _s.Player.Jumping = true;
            _s.Player.JumpTime = 0;
            _s.Sounds.Add("jump");
        }
    }

    public void ActionStart()
    {
        if (_s.Phase == GamePhase.Menu)
            StartGame();
    }

    public void ActionRestart()
    {
        if (_s.Phase == GamePhase.GameOver)
            ResetToMenu();
    }

    // ─── Game lifecycle ─────────────────────────────
    private void ResetToMenu()
    {
        _s.Phase = GamePhase.Menu;
        _s.Score = 0;
        _s.Speed = GameConfig.SpeedInitial;
        _s.Scroll = 0;
        _s.Time = 0;
        _s.Player.Lane = 0;
        _s.Player.Vx = 0;
        _s.Player.Jumping = false;
        _s.Player.JumpTime = 0;
        _s.Player.JumpY = 0;
        _s.Player.Leg = 0;
        _s.Trains.Clear();
        _s.Coins.Clear();
        _s.Particles.Clear();
        _s.TrainTimer = 1.0;
        _s.CoinTimer = 0.5;
        _s.ShakeTime = 0;
        _s.FlashAlpha = 0;
    }

    private void StartGame()
    {
        _s.Phase = GamePhase.Playing;
        _s.Score = 0;
        _s.Speed = GameConfig.SpeedInitial;
        _s.Scroll = 0;
        _s.Time = 0;
        _s.Trains.Clear();
        _s.Coins.Clear();
        _s.Particles.Clear();
        _s.TrainTimer = 1.0;
        _s.CoinTimer = 0.5;
    }

    // ─── Frame update ───────────────────────────────
    public void Update(double dt)
    {
        _s.Sounds.Clear();

        // Effects always tick
        if (_s.ShakeTime > 0) _s.ShakeTime = Math.Max(0, _s.ShakeTime - dt);
        if (_s.FlashAlpha > 0) _s.FlashAlpha = Math.Max(0, _s.FlashAlpha - dt * 4);

        // Particles
        foreach (var p in _s.Particles)
        {
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Life -= dt;
        }
        _s.Particles.RemoveAll(p => p.Life <= 0);

        if (_s.Phase != GamePhase.Playing)
            return;

        _s.Time += dt;
        _s.Speed = Math.Min(GameConfig.SpeedMax, _s.Speed + GameConfig.SpeedAccel * dt);
        _s.Score += (int)(_s.Speed * dt * 0.03);
        _s.Scroll += _s.Speed * dt;

        // Player lane lerp
        double targetX = _s.Player.Lane * GameConfig.LaneW;
        _s.Player.Vx += (targetX - _s.Player.Vx) * Math.Min(1, GameConfig.LaneLerp * dt);

        // Jump
        if (_s.Player.Jumping)
        {
            _s.Player.JumpTime += dt;
            if (_s.Player.JumpTime >= GameConfig.JumpDur)
            {
                _s.Player.Jumping = false;
                _s.Player.JumpTime = 0;
                _s.Player.JumpY = 0;
            }
            else
            {
                double t = _s.Player.JumpTime / GameConfig.JumpDur;
                _s.Player.JumpY = GameConfig.JumpH * Math.Sin(Math.PI * t);
            }
        }

        // Leg animation
        _s.Player.Leg += dt * _s.Speed * 0.018;

        // Spawn trains
        _s.TrainTimer -= dt;
        if (_s.TrainTimer <= 0)
        {
            SpawnTrain();
            double range = GameConfig.TrainGapMax - GameConfig.TrainGapMin;
            double speedRatio = GameConfig.SpeedInitial / _s.Speed;
            _s.TrainTimer = GameConfig.TrainGapMin + Rng.NextDouble() * range * speedRatio;
        }

        // Spawn coins
        _s.CoinTimer -= dt;
        if (_s.CoinTimer <= 0)
        {
            SpawnCoin();
            _s.CoinTimer = GameConfig.CoinGapMin + Rng.NextDouble() * (GameConfig.CoinGapMax - GameConfig.CoinGapMin);
        }

        // Move objects
        foreach (var tr in _s.Trains)
            tr.Z -= _s.Speed * dt;

        foreach (var c in _s.Coins)
        {
            c.Z -= _s.Speed * dt;
            c.Bob += dt * 5;
        }

        // Collision — trains
        bool airborne = _s.Player.Jumping && _s.Player.JumpY > GameConfig.JumpSafe;
        foreach (var tr in _s.Trains)
        {
            if (tr.Lane == _s.Player.Lane && tr.Z < GameConfig.TrainHitZ && tr.Z > 0 && !airborne)
            {
                TriggerGameOver();
                return;
            }
        }

        // Collision — coins
        foreach (var c in _s.Coins)
        {
            if (!c.Dead && c.Lane == _s.Player.Lane && c.Z < GameConfig.CoinHitZ && c.Z > 0)
            {
                c.Dead = true;
                _s.Score += GameConfig.CoinValue;
                _s.Sounds.Add("coin");
                BurstParticles(c);
            }
        }

        // Cleanup
        _s.Trains.RemoveAll(t => t.Z <= GameConfig.TrainKillZ);
        _s.Coins.RemoveAll(c => c.Z <= GameConfig.TrainKillZ || c.Dead);
    }

    // ─── Spawning ───────────────────────────────────
    private bool LaneBlocked(int lane, double z, double gap) =>
        _s.Trains.Exists(t => t.Lane == lane && Math.Abs(t.Z - z) < gap);

    private void SpawnTrain()
    {
        double diff = Math.Min(1, (_s.Speed - GameConfig.SpeedInitial) / (GameConfig.SpeedMax - GameConfig.SpeedInitial));
        double pairChance = 0.12 + diff * 0.28;

        if (Rng.NextDouble() < pairChance)
        {
            int safe = Rng.Next(3) - 1;
            if (!LaneBlocked(safe, GameConfig.TrainSpawnZ, GameConfig.TrainSafeZ))
            {
                for (int l = -1; l <= 1; l++)
                    if (l != safe)
                        _s.Trains.Add(MakeTrain(l));
                return;
            }
        }

        int lane = Rng.Next(3) - 1;
        if (!LaneBlocked(lane, GameConfig.TrainSpawnZ, GameConfig.TrainSafeZ))
            _s.Trains.Add(MakeTrain(lane));
    }

    private static Train MakeTrain(int lane) => new()
    {
        Lane = lane,
        Z = GameConfig.TrainSpawnZ,
        Color = GameConfig.TrainColors[Rng.Next(GameConfig.TrainColors.Length)]
    };

    private void SpawnCoin()
    {
        int lane = Rng.Next(3) - 1;
        _s.Coins.Add(new Coin
        {
            Lane = lane,
            Z = GameConfig.CoinSpawnZ,
            Bob = Rng.NextDouble() * Math.PI * 2,
            Dead = false
        });
    }

    // ─── Particles ──────────────────────────────────
    private void BurstParticles(Coin coin)
    {
        // Project coin to screen coords for particle origin
        double wz = coin.Z <= 0 ? 0.001 : coin.Z;
        double s = GameConfig.Fov / (GameConfig.Fov + wz);
        double vpX = GameConfig.Width / 2.0;
        double vpY = GameConfig.Height * GameConfig.VpYRatio;
        double wx = coin.Lane * GameConfig.LaneW;
        double px = vpX + wx * s;
        double py = vpY + (GameConfig.Height - vpY) * s;

        for (int i = 0; i < 10; i++)
        {
            double a = (Math.PI * 2 / 10) * i;
            _s.Particles.Add(new Particle
            {
                X = px,
                Y = py,
                Vx = Math.Cos(a) * (80 + Rng.NextDouble() * 60),
                Vy = Math.Sin(a) * (80 + Rng.NextDouble() * 60),
                Life = 0.4 + Rng.NextDouble() * 0.2,
                MaxLife = 0.6,
                Color = "#ffdd00",
                R = 3 + Rng.NextDouble() * 2
            });
        }
    }

    // ─── Game Over ──────────────────────────────────
    private void TriggerGameOver()
    {
        _s.Phase = GamePhase.GameOver;
        _s.ShakeTime = 0.35;
        _s.FlashAlpha = 1;
        _s.Sounds.Add("crash");
        if (_s.Score > _s.HighScore)
            _s.HighScore = _s.Score;
    }
}
