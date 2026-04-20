namespace SubwaySurfer.Entities;

public class Train
{
    public int    Lane  { get; set; }
    public double Z     { get; set; }
    public string Color { get; set; } = "#CC2222";

    private static readonly string[] Palette =
    [
        "#CC2222", "#2255CC", "#22AA44",
        "#CC7722", "#8833BB", "#CC2288"
    ];

    private static readonly Random Rng = new();

    public static Train Spawn(int lane) => new()
    {
        Lane  = lane,
        Z     = 2500,
        Color = Palette[Rng.Next(Palette.Length)],
    };
}