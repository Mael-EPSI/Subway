using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SubwaySurfer.Rendering;

public class SpriteManager
{
    private readonly Dictionary<string, BitmapSource> _cache = new();

    // Clés publiques
    public const string RunFrame0   = "player_run_0";
    public const string RunFrame1   = "player_run_1";
    public const string JumpSprite  = "player_jump";
    public const string StartScreen = "player_start_screen";
    public const string Death       = "player_death";
    public const string Chaser      = "chasseur";

    public void LoadAll()
    {
        TryLoad(RunFrame0,   "Assets/player_run_0.png");
        TryLoad(RunFrame1,   "Assets/player_run_1.png");
        TryLoad(JumpSprite,  "Assets/player_jump.png");
        TryLoad(StartScreen, "Assets/player_start_screen.png");
        TryLoad(Death,       "Assets/player_death.png");
        TryLoad(Chaser,      "Assets/chasseur.png");
    }

    private void TryLoad(string key, string relativePath)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            var bmp = new BitmapImage(uri);
            bmp.Freeze();
            _cache[key] = bmp;
        }
        catch { }
    }

    public bool Has(string key) => _cache.ContainsKey(key);

    public BitmapSource Get(string key, bool flipVertical = false)
    {
        if (!_cache.TryGetValue(key, out var src))
            throw new KeyNotFoundException(key);

        if (!flipVertical) return src;

        var transform = new ScaleTransform(1, -1, 0.5, 0.5);
        var flipped   = new TransformedBitmap(src, transform);
        flipped.Freeze();
        return flipped;
    }
}