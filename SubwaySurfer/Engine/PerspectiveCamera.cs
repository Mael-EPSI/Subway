namespace SubwaySurfer.Engine;

public record ScreenPoint(double X, double Y, double Scale);

public class PerspectiveCamera
{
    private const double Fov = 250.0;

    private readonly double _canvasW;
    private readonly double _canvasH;
    private readonly double _vpX;
    private readonly double _vpY;

    public PerspectiveCamera(double canvasW, double canvasH)
    {
        _canvasW = canvasW;
        _canvasH = canvasH;
        _vpX     = canvasW / 2.0;
        _vpY     = canvasH * 0.35;
    }

    public ScreenPoint Project(double worldX, double worldZ)
    {
        if (worldZ <= 0) worldZ = 0.001;
        double scale   = Fov / (Fov + worldZ);
        double screenX = _vpX + worldX * scale;
        double screenY = _vpY + (_canvasH - _vpY) * scale;
        return new ScreenPoint(screenX, screenY, scale);
    }

    /// Retourne la valeur Z monde qui projette à la screenY donnée.
    public double ZAtScreenY(double screenY)
    {
        double dy = screenY - _vpY;
        if (dy <= 0) return 0;
        double scale = dy / (_canvasH - _vpY);
        if (scale <= 0) return 0;
        return Fov / scale - Fov;
    }

    public double DepthAlpha(double z)
    {
        if (z > 2000) return 0.25;
        return 0.25 + 0.75 * (1.0 - z / 2000.0);
    }

    public double VpX => _vpX;
    public double VpY => _vpY;
    public double CanvasW => _canvasW;
    public double CanvasH => _canvasH;
}