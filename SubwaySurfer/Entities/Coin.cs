namespace SubwaySurfer.Entities;

public class Coin
{
    public int    Lane   { get; set; }
    public double Z      { get; set; }
    public double Bob    { get; set; }
    public bool   Dead   { get; set; }

    private static readonly Random Rng = new();

    public static Coin Spawn(int lane) => new()
    {
        Lane = lane,
        Z    = 2500,
        Bob  = Rng.NextDouble() * Math.PI * 2,
        Dead = false,
    };
}