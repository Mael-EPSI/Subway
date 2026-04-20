using System.Windows.Controls;
using System.Windows.Media;
using SubwaySurfer.Entities;
using SubwaySurfer.Rendering;

namespace SubwaySurfer.Engine;

public class Particle
{
    public double X, Y, Vx, Vy, Life, MaxLife, R;
}

public class GameEngine
{
    // ── Constantes ───────────────────────────────────────────
    private const double SpeedInit  = 500;
    private const double SpeedMax   = 1500;
    private const double SpeedAcc   = 8;
    private const double TrainKillZ = -30;
    private const double LaneW      = 110;
    private const double SafeZ      = 500;
    private const double PlayerBaseOffset = 160; // baseY = canvasH - 160

    // Calculés après construction de la caméra
    private double _hitZ;   // Z auquel le joueur se trouve visuellement

    // ── État ─────────────────────────────────────────────────
    private Phase  _phase = Phase.Menu;
    private int    _score;
    private int    _best;
    private double _speed;
    private double _scroll;
    private double _trainTimer;
    private double _coinTimer;
    private double _shakeT;
    private double _flashA;

    private readonly Player         _player    = new();
    private readonly List<Train>    _trains    = new();
    private readonly List<Coin>     _coins     = new();
    private readonly List<Particle> _particles = new();

    // ── Moteur ───────────────────────────────────────────────
    public  readonly InputManager      Input;
    public  readonly SpriteManager     Sprites;
    private readonly PerspectiveCamera _cam;
    private readonly Renderer          _renderer;
    private readonly AudioManager      _audio;

    // ── Callbacks UI ─────────────────────────────────────────
    private readonly Action           _showMenu;
    private readonly Action<int, int> _showGameOver;
    private readonly Action           _onStartGame;

    // ── Timing ───────────────────────────────────────────────
    private long   _lastTick = 0;
    private readonly Random _rng = new();

    public GameEngine(Canvas canvas, Action showMenu, Action<int, int> showGameOver, Action onStartGame)
    {
        _showMenu     = showMenu;
        _showGameOver = showGameOver;
        _onStartGame  = onStartGame;

        Input   = new InputManager();
        Sprites = new SpriteManager();
        Sprites.LoadAll();

        _cam      = new PerspectiveCamera(canvas.Width, canvas.Height);
        _renderer = new Renderer(canvas, _cam, Sprites);
        _audio    = new AudioManager();

        // Z correspondant à la position visuelle du joueur (baseY = canvasH - PlayerBaseOffset)
        _hitZ = _cam.ZAtScreenY(canvas.Height - PlayerBaseOffset);
    }

    // ── Boucle principale ────────────────────────────────────
    public void Tick()
    {
        long now = Environment.TickCount64;

        if (_lastTick == 0)
        {
            _lastTick = now;
            return;
        }

        double dt = Math.Min(0.05, (now - _lastTick) / 1000.0);
        _lastTick = now;

        Update(dt);
        Draw();
    }

    // ── Update ───────────────────────────────────────────────
    private void Update(double dt)
    {
        // Effets visuels toujours actifs
        if (_shakeT > 0) _shakeT = Math.Max(0, _shakeT - dt);
        if (_flashA > 0) _flashA = Math.Max(0, _flashA - dt * 4);

        // Particules
        foreach (var p in _particles)
        {
            p.X    += p.Vx * dt;
            p.Y    += p.Vy * dt;
            p.Life -= dt;
        }
        _particles.RemoveAll(p => p.Life <= 0);

        if (_phase != Phase.Playing) return;

        // Vitesse & score
        _speed  = Math.Min(SpeedMax, _speed + SpeedAcc * dt);
        _score += (int)(_speed * dt * 0.1);
        _scroll += _speed * dt;

        // Input → joueur
        if (Input.ConsumeLeft())  _player.MoveLeft();
        if (Input.ConsumeRight()) _player.MoveRight();
        if (Input.ConsumeJump())  { _player.Jump(); _audio.PlayJump(); }

        _player.Update(dt, _speed);

        // Spawn trains
        _trainTimer -= dt;
        if (_trainTimer <= 0)
        {
            SpawnTrain();
            double ratio = SpeedInit / _speed;
            _trainTimer  = 0.50 + _rng.NextDouble() * 1.10 * ratio;
        }

        // Spawn coins
        _coinTimer -= dt;
        if (_coinTimer <= 0)
        {
            _coins.Add(Coin.Spawn(_rng.Next(3) - 1));
            _coinTimer = 0.30 + _rng.NextDouble() * 0.60;
        }

        // Déplacer objets
        foreach (var t in _trains) t.Z -= _speed * dt;
        foreach (var c in _coins)  { c.Z -= _speed * dt; c.Bob += dt * 5; }

        // Collision trains
        bool airborne = _player.IsJumping && _player.JumpY > Player.JumpSafe;
        foreach (var t in _trains)
        {
            if (t.Lane == _player.Lane && t.Z < _hitZ && t.Z > 0 && !airborne)
            {
                TriggerGameOver();
                return;
            }
        }

        // Collision pièces
        foreach (var c in _coins)
        {
            if (!c.Dead && c.Lane == _player.Lane && c.Z < _hitZ && c.Z > 0)
            {
                c.Dead  = true;
                _score += 10;
                _audio.PlayCoin();
                BurstParticles(c);
            }
        }

        // Nettoyage
        _trains.RemoveAll(t => t.Z < TrainKillZ);
        _coins.RemoveAll(c  => c.Z < TrainKillZ || c.Dead);
    }

    // ── Spawn trains ─────────────────────────────────────────
    private void SpawnTrain()
    {
        double diff       = Math.Min(1, (_speed - SpeedInit) / (SpeedMax - SpeedInit));
        double pairChance = 0.12 + diff * 0.28;

        if (_rng.NextDouble() < pairChance)
        {
            int safe = _rng.Next(3) - 1;
            if (!LaneBlocked(safe))
            {
                for (int l = -1; l <= 1; l++)
                    if (l != safe) _trains.Add(Train.Spawn(l));
                return;
            }
        }

        int lane = _rng.Next(3) - 1;
        if (!LaneBlocked(lane))
            _trains.Add(Train.Spawn(lane));
    }

    private bool LaneBlocked(int lane) =>
        _trains.Exists(t => t.Lane == lane && Math.Abs(t.Z - 2500) < SafeZ);

    // ── Particules ───────────────────────────────────────────
    private void BurstParticles(Coin coin)
    {
        var p = _cam.Project(coin.Lane * LaneW, coin.Z);
        for (int i = 0; i < 10; i++)
        {
            double a = Math.PI * 2 / 10 * i;
            _particles.Add(new Particle
            {
                X       = p.X,
                Y       = p.Y,
                Vx      = Math.Cos(a) * (80 + _rng.NextDouble() * 60),
                Vy      = Math.Sin(a) * (80 + _rng.NextDouble() * 60),
                Life    = 0.4 + _rng.NextDouble() * 0.2,
                MaxLife = 0.6,
                R       = 3 + _rng.NextDouble() * 2,
            });
        }
    }

    // ── Game Over ────────────────────────────────────────────
    private void TriggerGameOver()
    {
        _phase  = Phase.GameOver;
        _shakeT = 0.35;
        _flashA = 1.0;

        if (_score > _best) _best = _score;
        _audio.StopMusic();
        _audio.PlayCrash();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            _showGameOver(_score, _best));
    }

    // ── Menu / Restart ───────────────────────────────────────
public void StartGame()
{
    _phase      = Phase.Playing;
    _score      = 0;
    _speed      = SpeedInit;
    _scroll     = 0;
    _trainTimer = 1.0;
    _coinTimer  = 0.5;
    _trains.Clear();
    _coins.Clear();
    _particles.Clear();
    _player.Reset();

    // Cache les overlays
    System.Windows.Application.Current.Dispatcher.Invoke(_onStartGame);
    _audio.PlayMusic();
}
    public void GoToMenu()
    {
        _phase = Phase.Menu;
        _player.Reset();
        _trains.Clear();
        _coins.Clear();
        _particles.Clear();
        System.Windows.Application.Current.Dispatcher.Invoke(_showMenu);
        _audio.StopMusic();
    }

    // ── Input global (Space/Enter pour changer de phase) ─────
    public void HandleGlobalKey(System.Windows.Input.Key key)
    {
        if (key is System.Windows.Input.Key.Space or
                   System.Windows.Input.Key.Enter)
        {
            if      (_phase == Phase.Menu)     StartGame();
            else if (_phase == Phase.GameOver) GoToMenu();
        }
    }

    // ── Draw ─────────────────────────────────────────────────
    private void Draw()
    {
        _renderer.DrawFrame(
            _scroll,
            _player,
            _trains,
            _coins,
            _particles,
            _shakeT,
            _flashA,
            _score,
            _best,
            _phase == Phase.Playing,
            _phase == Phase.GameOver);
    }
}

public enum Phase { Menu, Playing, GameOver }