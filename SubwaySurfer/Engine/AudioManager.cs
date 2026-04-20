using System;
using System.IO;
using System.Windows.Media;

namespace SubwaySurfer.Engine;

public class AudioManager
{
    private readonly MediaPlayer _music = new();
    private bool _musicLoaded = false;

    // Effets sonores (WAV)
    private readonly System.Media.SoundPlayer? _jumpSfx;
    private readonly System.Media.SoundPlayer? _coinSfx;
    private readonly System.Media.SoundPlayer? _crashSfx;

    public AudioManager()
    {
        // Musique de fond — lecture en boucle
        string musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Beat It.mp3");
        if (File.Exists(musicPath))
        {
            _music.Open(new Uri(musicPath, UriKind.Absolute));
            _music.MediaEnded += (_, _) =>
            {
                _music.Position = TimeSpan.Zero;
                _music.Play();
            };
            _music.Volume = 0.55;
            _musicLoaded  = true;
        }

        // SFX optionnels
        _jumpSfx  = TrySfx("Assets/jump.wav");
        _coinSfx  = TrySfx("Assets/coin.wav");
        _crashSfx = TrySfx("Assets/crash.wav");
    }

    // ── Musique ──────────────────────────────────────────────
    public void PlayMusic()
    {
        if (!_musicLoaded) return;
        _music.Position = TimeSpan.Zero;
        _music.Play();
    }

    public void StopMusic()
    {
        if (!_musicLoaded) return;
        _music.Stop();
    }

    public void PauseMusic()
    {
        if (!_musicLoaded) return;
        _music.Pause();
    }

    public void SetVolume(double v) => _music.Volume = Math.Clamp(v, 0, 1);

    // ── SFX ─────────────────────────────────────────────────
    public void PlayJump()  => TryPlay(_jumpSfx);
    public void PlayCoin()  => TryPlay(_coinSfx);
    public void PlayCrash() => TryPlay(_crashSfx);

    // ── Helpers ──────────────────────────────────────────────
    private static System.Media.SoundPlayer? TrySfx(string relativePath)
    {
        try
        {
            string full = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(full)) return null;
            var sp = new System.Media.SoundPlayer(full);
            sp.Load();
            return sp;
        }
        catch { return null; }
    }

    private static void TryPlay(System.Media.SoundPlayer? sp)
    {
        try { sp?.Play(); } catch { }
    }
}
