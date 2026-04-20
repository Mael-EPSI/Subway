using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SubwaySurfer.Engine;
using SubwaySurfer.Entities;

namespace SubwaySurfer.Rendering;

public class Renderer
{
    private const double TrackHw = 180.0;
    private const double MaxZ    = 3000.0;
    private const double TieSp   = 180.0;
    private const double LaneW   = 110.0;
    private const double TrainW  = 150.0;
    private const double TrainH  = 200.0;
    private const double CoinR   = 14.0;
    private const double FloatH  = 40.0;

    private readonly Canvas            _canvas;
    private readonly PerspectiveCamera _cam;
    private readonly SpriteManager     _sprites;

    // Pools séparés par type — plus de mélange
    private readonly List<Rectangle>                   _rects     = new();
    private readonly List<Ellipse>                     _ellipses  = new();
    private readonly List<System.Windows.Shapes.Line>  _lines     = new();
    private readonly List<Polygon>                     _polygons  = new();
    private readonly List<Image>                       _images    = new();
    private readonly List<TextBlock>                   _texts     = new();

    private int _ri, _ei, _li, _pi, _ii, _ti;

    public Renderer(Canvas canvas, PerspectiveCamera cam, SpriteManager sprites)
    {
        _canvas  = canvas;
        _cam     = cam;
        _sprites = sprites;
    }

    // ── Point d'entrée ───────────────────────────────────────
    public void DrawFrame(
    double scroll, Player player,
    List<Train> trains, List<Coin> coins, List<Particle> particles,
    double shakeT, double flashA, int score, int best,
    bool playing, bool gameOver)
{
    _ri = _ei = _li = _pi = _ii = _ti = 0;

    DrawSky();
    DrawWalls();
    DrawTrack();
    DrawTrackTies(scroll);
    DrawLaneLines(scroll);
    DrawWallWindows(scroll);

    var objects = trains.Select(t => (z: t.Z, draw: (Action)(() => DrawTrain(t))))
        .Concat(coins.Select(c => (z: c.Z, draw: (Action)(() => DrawCoin(c)))))
        .OrderByDescending(o => o.z);
    foreach (var o in objects) o.draw();

    // Chasseur visible pendant le jeu et au game over
    if (playing || gameOver) DrawChaser(scroll);

    // Joueur normal pendant le jeu, sprite mort au game over
    if (gameOver)       DrawDeathSprite(player);
    else                DrawPlayer(player);

    DrawParticles(particles);
    if (flashA > 0) DrawFlash(flashA);
    if (playing)    DrawHud(score, best);

    for (int i = _ri; i < _rects.Count;   i++) _rects[i].Visibility    = Visibility.Collapsed;
    for (int i = _ei; i < _ellipses.Count; i++) _ellipses[i].Visibility = Visibility.Collapsed;
    for (int i = _li; i < _lines.Count;    i++) _lines[i].Visibility    = Visibility.Collapsed;
    for (int i = _pi; i < _polygons.Count; i++) _polygons[i].Visibility = Visibility.Collapsed;
    for (int i = _ii; i < _images.Count;   i++) _images[i].Visibility   = Visibility.Collapsed;
    for (int i = _ti; i < _texts.Count;    i++) _texts[i].Visibility    = Visibility.Collapsed;
}

    // ── Ciel ─────────────────────────────────────────────────
    private void DrawSky()
    {
        var r = NextRect();
        r.Width = _cam.CanvasW; r.Height = _cam.VpY + 2;
        r.Fill  = new LinearGradientBrush(Color.FromRgb(6,6,26), Color.FromRgb(20,20,50), 90);
        Canvas.SetLeft(r, 0); Canvas.SetTop(r, 0);
    }

    // ── Murs ─────────────────────────────────────────────────
    private void DrawWalls()
    {
        var r = NextRect();
        r.Width = _cam.CanvasW; r.Height = _cam.CanvasH - _cam.VpY;
        r.Fill  = new LinearGradientBrush(Color.FromRgb(17,15,36), Color.FromRgb(26,24,64), 90);
        Canvas.SetLeft(r, 0); Canvas.SetTop(r, _cam.VpY);
    }

    // ── Voie ─────────────────────────────────────────────────
    private void DrawTrack()
    {
        var farL  = _cam.Project(-TrackHw, MaxZ);
        var farR  = _cam.Project( TrackHw, MaxZ);
        var nearL = _cam.Project(-TrackHw, 1);
        var nearR = _cam.Project( TrackHw, 1);

        var poly = NextPolygon();
        poly.Points = new PointCollection {
            new(farL.X,  farL.Y),
            new(farR.X,  farR.Y),
            new(nearR.X, Math.Min(nearR.Y, _cam.CanvasH)),
            new(nearL.X, Math.Min(nearL.Y, _cam.CanvasH)),
        };
        poly.Fill = new LinearGradientBrush(Color.FromRgb(34,34,34), Color.FromRgb(51,51,51), 90);
        poly.Stroke = null; poly.StrokeThickness = 0;
    }

    // ── Traverses ────────────────────────────────────────────
    private void DrawTrackTies(double scroll)
    {
        double off = scroll % TieSp;
        for (double z = off; z < MaxZ; z += TieSp)
        {
            if (z <= 1) continue;
            var l = _cam.Project(-TrackHw * 0.92, z);
            var r = _cam.Project( TrackHw * 0.92, z);
            double a  = _cam.DepthAlpha(z);
            var line  = NextLine();
            line.X1 = l.X; line.Y1 = l.Y; line.X2 = r.X; line.Y2 = r.Y;
            line.Stroke          = new SolidColorBrush(Color.FromArgb((byte)(a*180),80,65,45));
            line.StrokeThickness = Math.Max(1, 4 * l.Scale);
        }
    }

    // ── Lignes de voies ──────────────────────────────────────
    private void DrawLaneLines(double scroll)
    {
        double dashLen = 80, period = 160;
        foreach (double d in new[] { -0.5, 0.5 })
        {
            double wx  = d * LaneW;
            double off = scroll % period;
            for (double z = off; z < MaxZ; z += period)
            {
                if (z <= 1) continue;
                var p1   = _cam.Project(wx, z);
                var p2   = _cam.Project(wx, z + dashLen);
                double a = _cam.DepthAlpha(z);
                var line = NextLine();
                line.X1 = p1.X; line.Y1 = p1.Y; line.X2 = p2.X; line.Y2 = p2.Y;
                line.Stroke          = new SolidColorBrush(Color.FromArgb((byte)(a*150),255,200,0));
                line.StrokeThickness = Math.Max(1, 3 * p1.Scale);
            }
        }
    }

    // ── Fenêtres ─────────────────────────────────────────────
    private void DrawWallWindows(double scroll)
    {
        double sp = 300, off = scroll % sp;
        for (double z = off + 10; z < 2200; z += sp)
        {
            if (z <= 2) continue;
            var p     = _cam.Project(0, z);
            double a  = _cam.DepthAlpha(z);
            var lE    = _cam.Project(-TrackHw, z);
            var rE    = _cam.Project( TrackHw, z);
            double ws = Math.Max(2, 14 * p.Scale);

            for (int col = 1; col <= 3; col++)
            for (int row = 0; row < 4;  row++)
            {
                bool lit   = ((int)(z/sp)*7 + col*3 + row*11) % 5 > 1;
                byte alpha = (byte)(a * (lit ? 140 : 100));
                var color  = lit
                    ? Color.FromArgb(alpha,255,200,100)
                    : Color.FromArgb(alpha,35,30,55);

                double lx = lE.X - ws*col*1.8 - ws,  ly = lE.Y - ws*(row+1)*1.6;
                double rx = rE.X + ws*(col-1)*1.8 + ws*0.5, ry = rE.Y - ws*(row+1)*1.6;

                if (lx > 0 && ly > _cam.VpY)
                { var wr=NextRect(); wr.Width=ws; wr.Height=ws*1.2; wr.Fill=new SolidColorBrush(color); Canvas.SetLeft(wr,lx); Canvas.SetTop(wr,ly); }
                if (rx < _cam.CanvasW && ry > _cam.VpY)
                { var wr=NextRect(); wr.Width=ws; wr.Height=ws*1.2; wr.Fill=new SolidColorBrush(color); Canvas.SetLeft(wr,rx); Canvas.SetTop(wr,ry); }
            }
        }
    }

    // ── Train ────────────────────────────────────────────────
    private void DrawTrain(Train t)
    {
        var p = _cam.Project(t.Lane * LaneW, t.Z);
        double w = TrainW * p.Scale;
        double h = TrainH * p.Scale;
        if (w < 2 || p.Y < _cam.VpY - 5) return;

        double x  = p.X - w/2, y = p.Y - h;
        byte   al = (byte)(_cam.DepthAlpha(t.Z) * 255);
        var color = (Color)ColorConverter.ConvertFromString(t.Color);
        color.A   = al;

        var body = NextRect();
        body.Width=w; body.Height=h; body.Fill=new SolidColorBrush(color);
        body.RadiusX=body.RadiusY=Math.Max(2,6*p.Scale);
        body.Stroke=new SolidColorBrush(Color.FromArgb(al,0,0,0));
        body.StrokeThickness=Math.Max(1,2*p.Scale);
        Canvas.SetLeft(body,x); Canvas.SetTop(body,y);

        var ws=NextRect(); ws.Width=w-w*0.2; ws.Height=h*0.28;
        ws.Fill=new SolidColorBrush(Color.FromArgb(al,26,58,94));
        Canvas.SetLeft(ws,x+w*0.10); Canvas.SetTop(ws,y+h*0.06);

        if (w > 12)
        {
            double lr=Math.Max(2,w*0.065), hly=y+h*0.60;
            foreach (double lx in new[]{x+w*0.22, x+w*0.78})
            {
                var hl=NextEllipse(); hl.Width=hl.Height=lr*2;
                hl.Fill=new SolidColorBrush(Color.FromArgb(al,255,238,136));
                Canvas.SetLeft(hl,lx-lr); Canvas.SetTop(hl,hly-lr);
            }
        }

        var bumper=NextRect(); bumper.Width=w*0.88; bumper.Height=h*0.045;
        bumper.Fill=new SolidColorBrush(Color.FromArgb(al,85,85,85));
        Canvas.SetLeft(bumper,x+w*0.06); Canvas.SetTop(bumper,y+h*0.93);
    }

    // ── Pièce ────────────────────────────────────────────────
    private void DrawCoin(Coin c)
    {
        var p     = _cam.Project(c.Lane * LaneW, c.Z);
        double r  = CoinR * p.Scale;
        if (r < 1) return;
        double cy = p.Y - FloatH*p.Scale + Math.Sin(c.Bob)*4*p.Scale;
        byte   al = (byte)(_cam.DepthAlpha(c.Z) * 255);

        var coin=NextEllipse(); coin.Width=coin.Height=r*2;
        coin.Fill=new SolidColorBrush(Color.FromArgb(al,255,215,0));
        Canvas.SetLeft(coin,p.X-r); Canvas.SetTop(coin,cy-r);

        var inner=NextEllipse(); inner.Width=inner.Height=r*1.2;
        inner.Fill=new SolidColorBrush(Color.FromArgb(al,255,237,74));
        Canvas.SetLeft(inner,p.X-r*0.6); Canvas.SetTop(inner,cy-r*0.6);
    }

    // ── Joueur ───────────────────────────────────────────────
    private void DrawPlayer(Player player)
{
    double baseY  = _cam.CanvasH - 160;
    // Applique la même échelle perspective que les lignes de voies au niveau du joueur
    double pScale = (baseY - _cam.VpY) / (_cam.CanvasH - _cam.VpY);
    double px     = _cam.VpX + player.Vx * pScale;
    double py     = baseY - player.JumpY;

    bool   flipV = player.IsJumping;
    string key   = flipV
        ? SpriteManager.JumpSprite
        : (player.RunFrame == 0 ? SpriteManager.RunFrame0 : SpriteManager.RunFrame1);

    if (_sprites.Has(key))
    {
        var img = NextImage();
        img.Source = _sprites.Get(key, flipV);
        img.Width  = 64;
        img.Height = 96;
        img.RenderTransformOrigin = new Point(0.5, 0.5);
        img.RenderTransform = flipV
            ? new ScaleTransform(1, -1)
            : Transform.Identity;
        Canvas.SetLeft(img, px - 32);
        Canvas.SetTop(img,  py - 96);
    }
    else
    {
        double leg = player.LegAnim;
        double lL  = Math.Sin(leg)         * 10;
        double rL  = Math.Sin(leg+Math.PI) * 10;
        double lA  = Math.Sin(leg+Math.PI) * 8;
        double rA  = Math.Sin(leg)         * 8;

        FRect(px-10, py-26+lL,  8, 26, Color.FromRgb(29,53,87));
        FRect(px-11, py- 2+lL, 10,  5, Color.FromRgb(34,34,34));
        FRect(px+ 2, py-26+rL,  8, 26, Color.FromRgb(29,53,87));
        FRect(px+ 1, py- 2+rL, 10,  5, Color.FromRgb(34,34,34));
        FRect(px-16, py-62,    32, 38, Color.FromRgb(230,57,70));
        FRect(px-22, py-58+lA,  7, 22, Color.FromRgb(230,57,70));
        FRect(px+15, py-58+rA,  7, 22, Color.FromRgb(230,57,70));
        var head = NextEllipse();
        head.Width = head.Height = 24;
        head.Fill  = new SolidColorBrush(Color.FromRgb(43,45,66));
        Canvas.SetLeft(head, px-12);
        Canvas.SetTop(head,  py-85);
    }
}

public void DrawDeathSprite(Player player)
{
    double baseY  = _cam.CanvasH - 160;
    double pScale = (baseY - _cam.VpY) / (_cam.CanvasH - _cam.VpY);
    double px     = _cam.VpX + player.Vx * pScale;

    if (!_sprites.Has(SpriteManager.Death)) return;

    var img = NextImage();
    img.Source = _sprites.Get(SpriteManager.Death);
    img.Width  = 64;
    img.Height = 96;
    img.RenderTransform = Transform.Identity;
    Canvas.SetLeft(img, px - 32);
    Canvas.SetTop(img,  baseY - 96);
}

public void DrawChaser(double scroll)
{
    // Oscillation latérale + légère pompe verticale
    double wobble = Math.Sin(scroll * 0.015) * 18;
    double pump   = Math.Abs(Math.Sin(scroll * 0.04)) * 8;
    double px     = _cam.VpX + wobble;

    // Le chasseur est plus grand que le joueur (100×150), calé juste en bas
    double cw = 100, ch = 150;
    double cx = px - cw / 2;
    double cy = _cam.CanvasH - ch - 10 + pump;   // 10 px de marge en bas

    if (_sprites.Has(SpriteManager.Chaser))
    {
        var img = NextImage();
        img.Source = _sprites.Get(SpriteManager.Chaser);
        img.Width  = cw;
        img.Height = ch;
        img.RenderTransform = Transform.Identity;
        Canvas.SetLeft(img, cx);
        Canvas.SetTop(img,  cy);
    }
    else
    {
        // Fallback vectoriel : silhouette rouge menaçante
        double bx = px, by = _cam.CanvasH + 20 - ch + pump;
        // corps
        FRect(bx - 18, by + 40, 36, 55, Color.FromRgb(180, 20, 20));
        // tête
        var head = NextEllipse(); head.Width = 30; head.Height = 30;
        head.Fill = new SolidColorBrush(Color.FromRgb(200, 80, 50));
        Canvas.SetLeft(head, bx - 15); Canvas.SetTop(head, by + 10);
        // bras gauche
        FRect(bx - 34, by + 48, 16, 8, Color.FromRgb(180, 20, 20));
        // bras droit
        FRect(bx + 18, by + 48, 16, 8, Color.FromRgb(180, 20, 20));
        // jambe gauche
        FRect(bx - 14, by + 95, 12, 30, Color.FromRgb(40, 20, 20));
        // jambe droite
        FRect(bx + 2,  by + 95, 12, 30, Color.FromRgb(40, 20, 20));
    }
}
    private void FRect(double x,double y,double w,double h,Color c)
    {
        var r=NextRect(); r.Width=w; r.Height=h;
        r.Fill=new SolidColorBrush(c);
        Canvas.SetLeft(r,x); Canvas.SetTop(r,y);
    }

    // ── Particules ───────────────────────────────────────────
    private void DrawParticles(List<Particle> particles)
    {
        foreach (var p in particles)
        {
            double frac=Math.Max(0,p.Life/p.MaxLife), r=p.R*frac;
            var el=NextEllipse(); el.Width=el.Height=r*2;
            el.Fill=new SolidColorBrush(Color.FromArgb((byte)(frac*255),255,221,0));
            Canvas.SetLeft(el,p.X-r); Canvas.SetTop(el,p.Y-r);
        }
    }

    // ── Flash ────────────────────────────────────────────────
    private void DrawFlash(double flashA)
    {
        var r=NextRect(); r.Width=_cam.CanvasW; r.Height=_cam.CanvasH;
        r.Fill=new SolidColorBrush(Color.FromArgb((byte)(flashA*0.35*255),255,50,50));
        Canvas.SetLeft(r,0); Canvas.SetTop(r,0);
    }

    // ── HUD ──────────────────────────────────────────────────
    private void DrawHud(int score, int best)
    {
        HudText($"SCORE  {score}", 18, 18);
        HudText($"BEST   {best}",  _cam.CanvasW-170, 18);
    }

    private void HudText(string text, double x, double y)
    {
        var tb=NextText(); tb.Text=text; tb.FontSize=18;
        tb.FontWeight=FontWeights.Bold;
        tb.FontFamily=new FontFamily("Courier New");
        tb.Foreground=Brushes.White;
        Canvas.SetLeft(tb,x); Canvas.SetTop(tb,y);
    }

    // ── Pools typés ──────────────────────────────────────────
    private Rectangle NextRect()
    {
        if (_ri == _rects.Count)
        { var r=new Rectangle(); _canvas.Children.Add(r); _rects.Add(r); }
        var rect=_rects[_ri++];
        rect.Visibility=Visibility.Visible;
        rect.RadiusX=rect.RadiusY=0;
        rect.Stroke=null; rect.StrokeThickness=0;
        return rect;
    }

    private Ellipse NextEllipse()
    {
        if (_ei == _ellipses.Count)
        { var e=new Ellipse(); _canvas.Children.Add(e); _ellipses.Add(e); }
        var el=_ellipses[_ei++];
        el.Visibility=Visibility.Visible;
        return el;
    }

    private System.Windows.Shapes.Line NextLine()
    {
        if (_li == _lines.Count)
        { var l=new System.Windows.Shapes.Line(); _canvas.Children.Add(l); _lines.Add(l); }
        var line=_lines[_li++];
        line.Visibility=Visibility.Visible;
        return line;
    }

    private Polygon NextPolygon()
    {
        if (_pi == _polygons.Count)
        { var p=new Polygon(); _canvas.Children.Add(p); _polygons.Add(p); }
        var poly=_polygons[_pi++];
        poly.Visibility=Visibility.Visible;
        return poly;
    }

    private Image NextImage()
    {
        if (_ii == _images.Count)
        { var img=new Image{Stretch=Stretch.Fill}; _canvas.Children.Add(img); _images.Add(img); }
        var image=_images[_ii++];
        image.Visibility=Visibility.Visible;
        image.RenderTransform=Transform.Identity;
        image.RenderTransformOrigin=new Point(0.5,0.5);
        return image;
    }

    private TextBlock NextText()
    {
        if (_ti == _texts.Count)
        { var tb=new TextBlock(); _canvas.Children.Add(tb); _texts.Add(tb); }
        var t=_texts[_ti++];
        t.Visibility=Visibility.Visible;
        return t;
    }
}