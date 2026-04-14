namespace SubwaySurfer.Game;

public sealed class Train
{
    public int Lane { get; set; }
    public double Z { get; set; }
    public string Color { get; set; } = "#cc2222";
}

public sealed class Coin
{
    public int Lane { get; set; }
    public double Z { get; set; }
    public double Bob { get; set; }
    public bool Dead { get; set; }
}

public sealed class Particle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Life { get; set; }
    public double MaxLife { get; set; }
    public string Color { get; set; } = "#ffdd00";
    public double R { get; set; }
}

public sealed class PlayerState
{
    public int Lane { get; set; }
    public double Vx { get; set; }
    public bool Jumping { get; set; }
    public double JumpTime { get; set; }
    public double JumpY { get; set; }
    public double Leg { get; set; }
}
