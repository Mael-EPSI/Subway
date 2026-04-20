namespace SubwaySurfer.Entities;

public class Player
{
    // Voies : -1 gauche, 0 centre, 1 droite
    public int    Lane      { get; private set; } = 0;
    public double Vx        { get; private set; } = 0;
    public bool   IsJumping { get; private set; } = false;
    public double JumpT     { get; private set; } = 0;
    public double JumpY     { get; private set; } = 0;
    public double LegAnim   { get; private set; } = 0;
    public int    RunFrame  { get; private set; } = 0; // 0 ou 1

    private const double LaneWidth   = 110.0;
    private const double LaneLerp    = 12.0;
    private const double JumpDur     = 0.50;
    private const double JumpHeight  = 130.0;
    public  const double JumpSafe    = 30.0;

    private double _frameTimer = 0;

    public void MoveLeft()
    {
        if (Lane > -1) Lane--;
    }

    public void MoveRight()
    {
        if (Lane < 1) Lane++;
    }

    public void Jump()
    {
        if (!IsJumping)
        {
            IsJumping = true;
            JumpT     = 0;
        }
    }

    public void Update(double dt, double speed)
    {
        // Lerp position horizontale
        double targetX = Lane * LaneWidth;
        Vx += (targetX - Vx) * Math.Min(1.0, LaneLerp * dt);

        // Saut
        if (IsJumping)
        {
            JumpT += dt;
            if (JumpT >= JumpDur)
            {
                IsJumping = false;
                JumpT     = 0;
                JumpY     = 0;
            }
            else
            {
                double t = JumpT / JumpDur;
                JumpY = JumpHeight * Math.Sin(Math.PI * t);
            }
        }

        // Animation jambes
        LegAnim += dt * speed * 0.018;

        // Frame sprite (alterne à ~8fps)
        _frameTimer += dt;
        if (_frameTimer >= 0.125)
        {
            _frameTimer = 0;
            RunFrame    = RunFrame == 0 ? 1 : 0;
        }
    }

    public void Reset()
    {
        Lane      = 0;
        Vx        = 0;
        IsJumping = false;
        JumpT     = 0;
        JumpY     = 0;
        LegAnim   = 0;
        RunFrame  = 0;
    }
}