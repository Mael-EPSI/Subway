using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SubwaySurfer.Engine;
using SubwaySurfer.Rendering;

namespace SubwaySurfer;

public partial class MainWindow : Window
{
    private readonly GameEngine _engine;

public MainWindow()
{
    InitializeComponent();
    _engine = new GameEngine(GameCanvas, ShowMenu, ShowGameOver, HideOverlays); // ← ajoute HideOverlays
    CompositionTarget.Rendering += OnRendering;

    if (_engine.Sprites.Has(SpriteManager.StartScreen))
        StartScreenImg.Source = _engine.Sprites.Get(SpriteManager.StartScreen);
}

private void HideOverlays()
{
    MenuOverlay.Visibility     = Visibility.Collapsed;
    GameOverOverlay.Visibility = Visibility.Collapsed;
    StartScreenImg.Visibility  = Visibility.Collapsed; // ← disparaît au démarrage
}

    private void OnRendering(object? sender, EventArgs e)
    {
        _engine.Tick();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _engine.Input.KeyDown(e.Key);
        _engine.HandleGlobalKey(e.Key);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        _engine.Input.KeyUp(e.Key);
    }

private void ShowMenu()
{
    MenuOverlay.Visibility     = Visibility.Visible;
    GameOverOverlay.Visibility = Visibility.Collapsed;
    StartScreenImg.Visibility  = Visibility.Visible; // ← réaffiche au menu
}

private void ShowGameOver(int score, int best)
{
    FinalScoreText.Text        = $"Score : {score}";
    BestScoreText.Text         = $"Meilleur : {best}";
    GameOverOverlay.Visibility = Visibility.Visible;
    MenuOverlay.Visibility     = Visibility.Collapsed;
    StartScreenImg.Visibility  = Visibility.Collapsed; // ← cache au game over
}
}