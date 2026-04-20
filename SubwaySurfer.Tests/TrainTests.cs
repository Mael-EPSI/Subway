using SubwaySurfer.Entities;
using Xunit;

namespace SubwaySurfer.Tests;

public class TrainTests
{
    // ── Spawn Z ──────────────────────────────────────────

    [Fact]
    public void Spawn_SetsZ_To2500()
    {
        var t = Train.Spawn(0);
        Assert.Equal(2500, t.Z);
    }

    // ── Spawn Color ──────────────────────────────────────

    [Fact]
    public void Spawn_Color_IsNotNullOrEmpty()
    {
        var t = Train.Spawn(0);
        Assert.False(string.IsNullOrWhiteSpace(t.Color));
    }

    [Fact]
    public void Spawn_Color_LooksLikeHexOrNamedColor()
    {
        var t = Train.Spawn(0);
        // Accept both "#RRGGBB" style and any non-empty string
        Assert.NotNull(t.Color);
        Assert.True(t.Color.Length >= 4);
    }

    // ── Spawn Lane ───────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Spawn_AssignsCorrectLane(int lane)
    {
        var t = Train.Spawn(lane);
        Assert.Equal(lane, t.Lane);
    }

    // ── Randomness ───────────────────────────────────────

    [Fact]
    public void Spawn_MultipleCallsCanProduceDifferentColors()
    {
        // Over 20 spawns we expect at least 2 distinct colors
        var colors = new HashSet<string>();
        for (int i = 0; i < 20; i++)
            colors.Add(Train.Spawn(0).Color);

        Assert.True(colors.Count > 1,
            "Expected Train.Spawn to produce more than one distinct color across 20 calls");
    }

    // ── Mutability ───────────────────────────────────────

    [Fact]
    public void Train_Z_CanBeDecremented()
    {
        var t = Train.Spawn(0);
        t.Z -= 100;
        Assert.Equal(2400, t.Z);
    }
}
