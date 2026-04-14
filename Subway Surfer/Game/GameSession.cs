using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubwaySurfer.Game;

public sealed class GameSession
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly WebSocket _ws;
    private readonly GameEngine _engine = new();
    private readonly CancellationTokenSource _cts = new();

    public GameSession(WebSocket ws) => _ws = ws;

    public async Task RunAsync()
    {
        var recvTask = ReceiveInputAsync();
        var sendTask = GameLoopAsync();
        await Task.WhenAny(recvTask, sendTask);
        _cts.Cancel();
    }

    private async Task ReceiveInputAsync()
    {
        var buf = new byte[128];
        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(buf, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var msg = Encoding.UTF8.GetString(buf, 0, result.Count).Trim();
                switch (msg)
                {
                    case "left":    _engine.ActionLeft();    break;
                    case "right":   _engine.ActionRight();   break;
                    case "jump":    _engine.ActionJump();    break;
                    case "start":   _engine.ActionStart();   break;
                    case "restart": _engine.ActionRestart(); break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private async Task GameLoopAsync()
    {
        const double TargetDt = 1.0 / 60.0;
        var sw = Stopwatch.StartNew();
        double lastTime = sw.Elapsed.TotalSeconds;

        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                double now = sw.Elapsed.TotalSeconds;
                double dt = now - lastTime;
                lastTime = now;

                if (dt > 0.1) dt = 0.1; // clamp large spikes

                _engine.Update(dt);

                var snapshot = BuildSnapshot();
                var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOpts);
                await _ws.SendAsync(json, WebSocketMessageType.Text, true, _cts.Token);

                double elapsed = sw.Elapsed.TotalSeconds - now;
                double sleep = TargetDt - elapsed;
                if (sleep > 0)
                    await Task.Delay(TimeSpan.FromSeconds(sleep), _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private FrameSnapshot BuildSnapshot()
    {
        var s = _engine.State;
        return new FrameSnapshot
        {
            Phase = s.Phase switch
            {
                GamePhase.Menu => "menu",
                GamePhase.Playing => "playing",
                GamePhase.GameOver => "gameover",
                _ => "menu"
            },
            Score = s.Score,
            HighScore = s.HighScore,
            Speed = s.Speed,
            Scroll = s.Scroll,
            ShakeT = s.ShakeTime,
            FlashA = s.FlashAlpha,
            Player = new PlayerSnapshot
            {
                Lane = s.Player.Lane,
                Vx = s.Player.Vx,
                Jumping = s.Player.Jumping,
                JumpY = s.Player.JumpY,
                Leg = s.Player.Leg
            },
            Trains = s.Trains.Select(t => new TrainSnapshot
            {
                Lane = t.Lane,
                Z = t.Z,
                Color = t.Color
            }).ToList(),
            Coins = s.Coins.Select(c => new CoinSnapshot
            {
                Lane = c.Lane,
                Z = c.Z,
                Bob = c.Bob
            }).ToList(),
            Particles = s.Particles.Select(p => new ParticleSnapshot
            {
                X = p.X,
                Y = p.Y,
                Vx = p.Vx,
                Vy = p.Vy,
                Life = p.Life,
                MaxLife = p.MaxLife,
                Color = p.Color,
                R = p.R
            }).ToList(),
            Sounds = s.Sounds.Count > 0 ? s.Sounds.ToList() : null
        };
    }
}

// ─── DTOs ───────────────────────────────────────
sealed class FrameSnapshot
{
    public string Phase { get; set; } = "";
    public int Score { get; set; }
    public int HighScore { get; set; }
    public double Speed { get; set; }
    public double Scroll { get; set; }
    public double ShakeT { get; set; }
    public double FlashA { get; set; }
    public PlayerSnapshot Player { get; set; } = new();
    public List<TrainSnapshot> Trains { get; set; } = [];
    public List<CoinSnapshot> Coins { get; set; } = [];
    public List<ParticleSnapshot> Particles { get; set; } = [];
    public List<string>? Sounds { get; set; }
}

sealed class PlayerSnapshot
{
    public int Lane { get; set; }
    public double Vx { get; set; }
    public bool Jumping { get; set; }
    public double JumpY { get; set; }
    public double Leg { get; set; }
}

sealed class TrainSnapshot
{
    public int Lane { get; set; }
    public double Z { get; set; }
    public string Color { get; set; } = "";
}

sealed class CoinSnapshot
{
    public int Lane { get; set; }
    public double Z { get; set; }
    public double Bob { get; set; }
}

sealed class ParticleSnapshot
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Life { get; set; }
    public double MaxLife { get; set; }
    public string Color { get; set; } = "";
    public double R { get; set; }
}
