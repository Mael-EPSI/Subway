using Microsoft.JSInterop;
using SubwaySurfer.Game;

namespace SubwaySurfer.Rendering;

/// <summary>
/// Renders the game state to canvas via JS interop.
/// All rendering logic is in C# — JS is only a thin bridge.
/// </summary>
public sealed class GameRenderer
{
    private readonly IJSRuntime _js;

    private const double VpYRatio = 0.35;
    private const int W = GameConfig.Width;
    private const int H = GameConfig.Height;
    private static readonly double VpX = W / 2.0;
    private static readonly double VpY = Math.Floor(H * VpYRatio);

    // Pre-generated stars (deterministic for consistency)
    private readonly (double x, double y, double s, double b)[] _stars;

    public GameRenderer(IJSRuntime js)
    {
        _js = js;
        var rng = new Random(42);
        _stars = new (double, double, double, double)[60];
        for (int i = 0; i < _stars.Length; i++)
            _stars[i] = (rng.NextDouble(), rng.NextDouble(),
                         rng.NextDouble() * 1.5 + 0.5,
                         rng.NextDouble() * 0.5 + 0.3);
    }

    private static (double x, double y, double s) Project(double wx, double wz)
    {
        if (wz <= 0) wz = 0.001;
        double s = GameConfig.Fov / (GameConfig.Fov + wz);
        return (VpX + wx * s, VpY + (H - VpY) * s, s);
    }

    private static double DepthAlpha(double z) =>
        z > 2000 ? 0.25 : 0.25 + 0.75 * (1 - z / 2000);

    public async Task RenderFrame(GameState state)
    {
        var js = _js;
        await js.InvokeVoidAsync("GameInterop.clear");
        await js.InvokeVoidAsync("GameInterop.save");

        // Screen shake
        if (state.ShakeTime > 0)
        {
            var rng = Random.Shared;
            double m = state.ShakeTime * 25;
            await js.InvokeVoidAsync("GameInterop.translate",
                (rng.NextDouble() - 0.5) * m, (rng.NextDouble() - 0.5) * m);
        }

        await DrawSky(js);
        await DrawWalls(js);
        await DrawTrack(js);
        await DrawTrackTies(js, state);
        await DrawLaneLines(js, state);
        await DrawWallWindows(js, state);
        await DrawObjects(js, state);
        await DrawPlayer(js, state);
        await DrawParticles(js, state);
        await DrawFlash(js, state);

        await js.InvokeVoidAsync("GameInterop.restore");

        await DrawHUD(js, state);

        if (state.Phase == GamePhase.Menu)
            await DrawMenu(js);
        if (state.Phase == GamePhase.GameOver)
            await DrawGameOver(js, state);

        // Play sounds
        foreach (var snd in state.Sounds)
            await js.InvokeVoidAsync("GameInterop.playSound", snd);
    }

    // ─── Sky ────────────────────────────────────────
    private async Task DrawSky(IJSRuntime js)
    {
        await js.InvokeVoidAsync("GameInterop.linGrad",
            0, 0, 0, VpY, new object[] { 0, "#06061a", 1, "#141432" });
        await js.InvokeVoidAsync("GameInterop.fillGradRect", 0, 0, W, VpY + 1);

        foreach (var st in _stars)
            await js.InvokeVoidAsync("GameInterop.fillRect",
                st.x * W, st.y * VpY * 0.9, st.s, st.s,
                $"rgba(255,255,255,{st.b:F2})");

        await js.InvokeVoidAsync("GameInterop.radGrad",
            VpX, VpY, 0, 200,
            new object[] { 0, "rgba(60,50,120,0.35)", 1, "rgba(60,50,120,0)" });
        await js.InvokeVoidAsync("GameInterop.fillGradRect",
            VpX - 200, VpY - 100, 400, 200);
    }

    // ─── Walls ──────────────────────────────────────
    private async Task DrawWalls(IJSRuntime js)
    {
        await js.InvokeVoidAsync("GameInterop.linGrad",
            0, VpY, 0, H, new object[] { 0, "#110f24", 1, "#1a1840" });
        await js.InvokeVoidAsync("GameInterop.fillGradRect", 0, VpY, W, H - VpY);
    }

    // ─── Track ──────────────────────────────────────
    private async Task DrawTrack(IJSRuntime js)
    {
        double hw = GameConfig.TrackHW;
        var farL = Project(-hw, GameConfig.MaxZ);
        var farR = Project(hw, GameConfig.MaxZ);
        var nearL = Project(-hw, 1);
        var nearR = Project(hw, 1);

        await js.InvokeVoidAsync("GameInterop.linGrad",
            0, VpY, 0, H, new object[] { 0, "#222", 1, "#333" });

        await js.InvokeVoidAsync("GameInterop.polyFill", new double[]
        {
            farL.x, farL.y, farR.x, farR.y,
            nearR.x, Math.Min(nearR.y, H),
            nearL.x, Math.Min(nearL.y, H)
        });

        await DrawEdgeLine(js, -hw);
        await DrawEdgeLine(js, hw);
    }

    private async Task DrawEdgeLine(IJSRuntime js, double wx)
    {
        var pts = new List<double>();
        for (double z = 1; z < GameConfig.MaxZ; z += (z < 200 ? 10 : 50))
        {
            var p = Project(wx, z);
            pts.Add(p.x);
            pts.Add(p.y);
        }
        await js.InvokeVoidAsync("GameInterop.polyLine", pts.ToArray(), "#555", 1);
    }

    // ─── Track ties ─────────────────────────────────
    private async Task DrawTrackTies(IJSRuntime js, GameState state)
    {
        double sp = GameConfig.TieSpacing;
        double scroll = state.Scroll;
        double off = (sp - scroll % sp) % sp;
        await js.InvokeVoidAsync("GameInterop.setLineCap", "butt");

        for (double z = off; z < GameConfig.MaxZ; z += sp)
        {
            if (z <= 1) continue;
            var left = Project(-GameConfig.TrackHW * 0.92, z);
            var right = Project(GameConfig.TrackHW * 0.92, z);
            double a = DepthAlpha(z);
            await js.InvokeVoidAsync("GameInterop.line",
                left.x, left.y, right.x, right.y,
                $"rgba(80,65,45,{a:F2})", Math.Max(1, 4 * left.s));
        }
    }

    // ─── Lane lines ─────────────────────────────────
    private async Task DrawLaneLines(IJSRuntime js, GameState state)
    {
        const double dashLen = 80, gap = 80, period = dashLen + gap;
        double scroll = state.Scroll;

        for (double d = -0.5; d <= 0.5; d += 1)
        {
            double wx = d * GameConfig.LaneW;
            double off = (period - scroll % period) % period;

            for (double z = off; z < GameConfig.MaxZ; z += period)
            {
                if (z <= 1) continue;
                double z2 = z + dashLen;
                var p1 = Project(wx, z);
                var p2 = Project(wx, z2);
                double a = DepthAlpha(z);
                await js.InvokeVoidAsync("GameInterop.line",
                    p1.x, p1.y, p2.x, p2.y,
                    $"rgba(255,200,0,{a * 0.6:F2})", Math.Max(1, 3 * p1.s));
            }
        }
    }

    // ─── Wall windows ───────────────────────────────
    private async Task DrawWallWindows(IJSRuntime js, GameState state)
    {
        const double buildingSp = 300;
        double scroll = state.Scroll;
        double off = (buildingSp - scroll % buildingSp) % buildingSp;
        double hw = GameConfig.TrackHW;

        for (double z = off + 10; z < 2200; z += buildingSp)
        {
            if (z <= 2) continue;
            var p = Project(0, z);
            double a = DepthAlpha(z);
            var leftEdge = Project(-hw, z);
            var rightEdge = Project(hw, z);
            double ws = Math.Max(2, 14 * p.s);
            if (ws < 2) continue;

            for (int col = 1; col <= 3; col++)
            {
                for (int row = 0; row < 4; row++)
                {
                    bool lit = ((int)(z / buildingSp) * 7 + col * 3 + row * 11) % 5 > 1;
                    string c = lit
                        ? $"rgba(255,200,100,{a * 0.55:F2})"
                        : $"rgba(35,30,55,{a * 0.4:F2})";

                    double lx = leftEdge.x - ws * col * 1.8 - ws;
                    double ly = leftEdge.y - ws * (row + 1) * 1.6;
                    if (lx > 0 && ly > VpY)
                        await js.InvokeVoidAsync("GameInterop.fillRect",
                            lx, ly, ws, ws * 1.2, c);

                    double rx = rightEdge.x + ws * (col - 1) * 1.8 + ws * 0.5;
                    double ry = rightEdge.y - ws * (row + 1) * 1.6;
                    if (rx < W && ry > VpY)
                        await js.InvokeVoidAsync("GameInterop.fillRect",
                            rx, ry, ws, ws * 1.2, c);
                }
            }
        }
    }

    // ─── Game objects (sorted back-to-front) ────────
    private async Task DrawObjects(IJSRuntime js, GameState state)
    {
        var all = new List<(bool isTrain, double z, int idx)>();
        for (int i = 0; i < state.Trains.Count; i++)
            all.Add((true, state.Trains[i].Z, i));
        for (int i = 0; i < state.Coins.Count; i++)
            all.Add((false, state.Coins[i].Z, i));

        all.Sort((a, b) => b.z.CompareTo(a.z));

        foreach (var obj in all)
        {
            if (obj.isTrain)
                await DrawTrain(js, state.Trains[obj.idx]);
            else
                await DrawCoin(js, state.Coins[obj.idx]);
        }
    }

    // ─── Train ──────────────────────────────────────
    private async Task DrawTrain(IJSRuntime js, Train t)
    {
        double wx = t.Lane * GameConfig.LaneW;
        var p = Project(wx, t.Z);
        double w = GameConfig.TrainW * p.s;
        double h = GameConfig.TrainH * p.s;
        if (w < 2 || p.y < VpY - 5) return;

        double x = p.x - w / 2;
        double y = p.y - h;
        double a = DepthAlpha(t.Z);
        await js.InvokeVoidAsync("GameInterop.setAlpha", a);

        // Body
        await js.InvokeVoidAsync("GameInterop.roundRect",
            x, y, w, h, Math.Max(2, 6 * p.s),
            t.Color, true, true, "rgba(0,0,0,0.5)", Math.Max(1, 2 * p.s));

        // Windshield
        double wsM = w * 0.10;
        double wsY = y + h * 0.06;
        double wsH = h * 0.28;
        await js.InvokeVoidAsync("GameInterop.fillRect",
            x + wsM, wsY, w - wsM * 2, wsH, "#1a3a5e");
        await js.InvokeVoidAsync("GameInterop.fillRect",
            x + wsM + w * 0.05, wsY + wsH * 0.15, w * 0.35, wsH * 0.4,
            "rgba(120,170,220,0.25)");

        // Headlights
        if (w > 12)
        {
            double lr = Math.Max(2, w * 0.065);
            double hly = y + h * 0.60;

            await js.InvokeVoidAsync("GameInterop.fillCircle", x + w * 0.22, hly, lr, "#ffee88");
            await js.InvokeVoidAsync("GameInterop.fillCircle", x + w * 0.22, hly, lr * 2.2, "rgba(255,238,136,0.25)");
            await js.InvokeVoidAsync("GameInterop.fillCircle", x + w * 0.78, hly, lr, "#ffee88");
            await js.InvokeVoidAsync("GameInterop.fillCircle", x + w * 0.78, hly, lr * 2.2, "rgba(255,238,136,0.25)");
        }

        // Grill lines
        if (w > 18)
        {
            for (int i = 0; i < 3; i++)
            {
                double gy = y + h * (0.73 + i * 0.055);
                await js.InvokeVoidAsync("GameInterop.line",
                    x + w * 0.18, gy, x + w * 0.82, gy,
                    "rgba(0,0,0,0.4)", Math.Max(1, 1.5 * p.s));
            }
        }

        // Roof sign
        if (w > 20)
            await js.InvokeVoidAsync("GameInterop.fillRect",
                x + w * 0.28, y + h * 0.015, w * 0.44, h * 0.04, "#1a1a1a");

        // Bumper
        await js.InvokeVoidAsync("GameInterop.fillRect",
            x + w * 0.06, y + h * 0.93, w * 0.88, h * 0.045, "#555");

        await js.InvokeVoidAsync("GameInterop.setAlpha", 1);
    }

    // ─── Coin ───────────────────────────────────────
    private async Task DrawCoin(IJSRuntime js, Coin c)
    {
        double wx = c.Lane * GameConfig.LaneW;
        var p = Project(wx, c.Z);
        double r = GameConfig.CoinR * p.s;
        if (r < 1) return;

        double floatY = GameConfig.CoinFloatH * p.s;
        double bob = Math.Sin(c.Bob) * 4 * p.s;
        double cy = p.y - floatY + bob;
        double a = DepthAlpha(c.Z);
        await js.InvokeVoidAsync("GameInterop.setAlpha", a);

        // Shadow
        await js.InvokeVoidAsync("GameInterop.fillEllipse",
            p.x, p.y - 2 * p.s, r * 0.8, r * 0.3, "rgba(0,0,0,0.2)");
        // Outer
        await js.InvokeVoidAsync("GameInterop.fillCircle", p.x, cy, r, "#ffd700");
        // Inner
        await js.InvokeVoidAsync("GameInterop.fillCircle", p.x, cy, r * 0.6, "#ffed4a");

        if (r > 5)
            await js.InvokeVoidAsync("GameInterop.fillText",
                "$", p.x, cy + 1, "#b8860b",
                $"bold {Math.Max(8, (int)(r * 1.2))}px sans-serif",
                "center", "middle");

        await js.InvokeVoidAsync("GameInterop.setAlpha", 1);
    }

    // ─── Player ─────────────────────────────────────
    private async Task DrawPlayer(IJSRuntime js, GameState state)
    {
        var pl = state.Player;
        double baseY = H - 45;
        double px = VpX + pl.Vx;
        double py = baseY - pl.JumpY;
        double leg = pl.Leg;
        double lL = Math.Sin(leg) * 10;
        double rL = Math.Sin(leg + Math.PI) * 10;
        double lA = Math.Sin(leg + Math.PI) * 8;
        double rA = Math.Sin(leg) * 8;

        await js.InvokeVoidAsync("GameInterop.save");
        await js.InvokeVoidAsync("GameInterop.translate", px, py);

        // Jump shadow
        if (pl.Jumping)
        {
            double sh = 1 - (pl.JumpY / GameConfig.JumpH) * 0.4;
            await js.InvokeVoidAsync("GameInterop.fillEllipse",
                0, baseY - py + 2, 18 * sh, 5, "rgba(0,0,0,0.25)");
        }

        // Legs
        await js.InvokeVoidAsync("GameInterop.fillRect", -10, -26 + lL, 8, 26, "#1d3557");
        await js.InvokeVoidAsync("GameInterop.fillRect", -11, -2 + lL, 10, 5, "#222");
        await js.InvokeVoidAsync("GameInterop.fillRect", 2, -26 + rL, 8, 26, "#1d3557");
        await js.InvokeVoidAsync("GameInterop.fillRect", 1, -2 + rL, 10, 5, "#222");

        // Torso
        await js.InvokeVoidAsync("GameInterop.fillRect", -16, -62, 32, 38, "#e63946");
        await js.InvokeVoidAsync("GameInterop.fillRect", -16, -48, 32, 4, "#c62e3b");

        // Arms
        await js.InvokeVoidAsync("GameInterop.fillRect", -22, -58 + lA, 7, 22, "#e63946");
        await js.InvokeVoidAsync("GameInterop.fillRect", -21, -38 + lA, 6, 6, "#f4c2a1");
        await js.InvokeVoidAsync("GameInterop.fillRect", 15, -58 + rA, 7, 22, "#e63946");
        await js.InvokeVoidAsync("GameInterop.fillRect", 16, -38 + rA, 6, 6, "#f4c2a1");

        // Head
        await js.InvokeVoidAsync("GameInterop.fillCircle", 0, -73, 12, "#2b2d42");

        // Hat (cap)
        await js.InvokeVoidAsync("GameInterop.fillEllipseArc",
            0, -80, 14, 5, Math.PI, Math.PI * 2, "#e63946");
        await js.InvokeVoidAsync("GameInterop.fillRect", -13, -83, 26, 5, "#e63946");
        await js.InvokeVoidAsync("GameInterop.fillRect", -9, -84, 18, 3, "#c62e3b");

        await js.InvokeVoidAsync("GameInterop.restore");
    }

    // ─── Particles ──────────────────────────────────
    private async Task DrawParticles(IJSRuntime js, GameState state)
    {
        foreach (var p in state.Particles)
        {
            double frac = Math.Max(0, p.Life / p.MaxLife);
            await js.InvokeVoidAsync("GameInterop.setAlpha", frac);
            await js.InvokeVoidAsync("GameInterop.fillCircle",
                p.X, p.Y, p.R * frac, p.Color);
        }
        await js.InvokeVoidAsync("GameInterop.setAlpha", 1);
    }

    // ─── Flash ──────────────────────────────────────
    private async Task DrawFlash(IJSRuntime js, GameState state)
    {
        if (state.FlashAlpha > 0)
            await js.InvokeVoidAsync("GameInterop.fillRect",
                0, 0, W, H,
                $"rgba(255,50,50,{state.FlashAlpha * 0.35:F2})");
    }

    // ─── HUD ────────────────────────────────────────
    private async Task DrawHUD(IJSRuntime js, GameState state)
    {
        if (state.Phase != GamePhase.Playing) return;
        await js.InvokeVoidAsync("GameInterop.save");

        // Backgrounds
        await js.InvokeVoidAsync("GameInterop.fillRect", 10, 10, 160, 36, "rgba(0,0,0,0.45)");
        await js.InvokeVoidAsync("GameInterop.fillRect", W - 170, 10, 160, 36, "rgba(0,0,0,0.45)");
        await js.InvokeVoidAsync("GameInterop.fillRect", W / 2.0 - 60, 10, 120, 36, "rgba(0,0,0,0.45)");

        // Score
        await js.InvokeVoidAsync("GameInterop.fillText",
            $"SCORE  {state.Score}", 18, 28, "#fff", "bold 18px monospace", "left", "middle");

        // Best
        await js.InvokeVoidAsync("GameInterop.fillText",
            $"BEST  {state.HighScore}", W - 18, 28, "#fff", "bold 18px monospace", "right", "middle");

        // Speed bar
        double spd01 = Math.Clamp((state.Speed - GameConfig.SpeedInitial) / (GameConfig.SpeedMax - GameConfig.SpeedInitial), 0, 1);
        await js.InvokeVoidAsync("GameInterop.fillText",
            "SPEED", W / 2.0, 22, "#fff", "bold 11px monospace", "center", "middle");
        double barX = W / 2.0 - 40;
        await js.InvokeVoidAsync("GameInterop.fillRect", barX, 32, 80, 8, "#333");
        int hue = (int)(120 - spd01 * 120);
        await js.InvokeVoidAsync("GameInterop.fillRect", barX, 32, 80 * spd01, 8, $"hsl({hue},80%,50%)");

        await js.InvokeVoidAsync("GameInterop.restore");
    }

    // ─── Menu ───────────────────────────────────────
    private async Task DrawMenu(IJSRuntime js)
    {
        await js.InvokeVoidAsync("GameInterop.fillRect",
            0, 0, W, H, "rgba(0,0,0,0.55)");
        await js.InvokeVoidAsync("GameInterop.save");

        await js.InvokeVoidAsync("GameInterop.fillText",
            "SUBWAY SURFER", VpX, H * 0.28, "#ffd700",
            "bold 52px sans-serif", "center", "middle");

        await js.InvokeVoidAsync("GameInterop.fillText",
            "Endless Runner", VpX, H * 0.36, "#aaa",
            "bold 18px sans-serif", "center", "middle");

        double now = await js.InvokeAsync<double>("GameInterop.now");
        double flash = (Math.Sin(now * 0.004) + 1) * 0.5;
        await js.InvokeVoidAsync("GameInterop.setAlpha", 0.5 + flash * 0.5);

        await js.InvokeVoidAsync("GameInterop.fillText",
            "Press SPACE or Tap to Start", VpX, H * 0.52, "#fff",
            "bold 22px sans-serif", "center", "middle");

        await js.InvokeVoidAsync("GameInterop.setAlpha", 1);

        await js.InvokeVoidAsync("GameInterop.fillText",
            "\u2190 \u2192 or A/D  \u2014  Switch Lane", VpX, H * 0.66,
            "#888", "15px monospace", "center", "middle");
        await js.InvokeVoidAsync("GameInterop.fillText",
            "\u2191 or W/SPACE \u2014 Jump", VpX, H * 0.72,
            "#888", "15px monospace", "center", "middle");
        await js.InvokeVoidAsync("GameInterop.fillText",
            "Swipe on mobile", VpX, H * 0.78,
            "#888", "15px monospace", "center", "middle");

        await js.InvokeVoidAsync("GameInterop.restore");
    }

    // ─── Game Over ──────────────────────────────────
    private async Task DrawGameOver(IJSRuntime js, GameState state)
    {
        await js.InvokeVoidAsync("GameInterop.fillRect",
            0, 0, W, H, "rgba(0,0,0,0.65)");
        await js.InvokeVoidAsync("GameInterop.save");

        await js.InvokeVoidAsync("GameInterop.fillText",
            "GAME OVER", VpX, H * 0.26, "#e63946",
            "bold 48px sans-serif", "center", "middle");

        await js.InvokeVoidAsync("GameInterop.fillText",
            $"Score: {state.Score}", VpX, H * 0.38, "#fff",
            "bold 28px monospace", "center", "middle");

        await js.InvokeVoidAsync("GameInterop.fillText",
            $"Best: {state.HighScore}", VpX, H * 0.46, "#ffd700",
            "20px monospace", "center", "middle");

        double now = await js.InvokeAsync<double>("GameInterop.now");
        double flash = (Math.Sin(now * 0.004) + 1) * 0.5;
        await js.InvokeVoidAsync("GameInterop.setAlpha", 0.5 + flash * 0.5);

        await js.InvokeVoidAsync("GameInterop.fillText",
            "Press SPACE or Tap to Restart", VpX, H * 0.88, "#fff",
            "bold 20px sans-serif", "center", "middle");

        await js.InvokeVoidAsync("GameInterop.setAlpha", 1);
        await js.InvokeVoidAsync("GameInterop.restore");
    }
}
