using SubwaySurfer.Entities;
using Xunit;

namespace SubwaySurfer.Tests;

public class CoinTests
{
    // ── Spawn Z ──────────────────────────────────────────

    [Fact]
    public void Spawn_SetsZ_To2500()
    {
        var c = Coin.Spawn(0);
        Assert.Equal(2500, c.Z);
    }

    // ── Spawn Dead ───────────────────────────────────────

    [Fact]
    public void Spawn_SetsDead_False()
    {
        var c = Coin.Spawn(0);
        Assert.False(c.Dead);
    }

    // ── Spawn Lane ───────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Spawn_AssignsCorrectLane(int lane)
    {
        var c = Coin.Spawn(lane);
        Assert.Equal(lane, c.Lane);
    }

    // ── Bob ──────────────────────────────────────────────

    [Fact]
    public void Spawn_Bob_IsBetween0And2PI()
    {
        // Run several times because Bob is random
        for (int i = 0; i < 50; i++)
        {
            var c = Coin.Spawn(0);
            Assert.True(c.Bob >= 0 && c.Bob <= Math.PI * 2,
                $"Bob={c.Bob} is out of [0, 2π]");
        }
    }

    [Fact]
    public void Spawn_Bob_IsInitialized_NotAlwaysZero()
    {
        // Over 20 spawns, Bob should not always be the same value
        var bobs = new HashSet<double>();
        for (int i = 0; i < 20; i++)
            bobs.Add(Coin.Spawn(0).Bob);

        Assert.True(bobs.Count > 1,
            "Bob should have random variation across multiple Spawn() calls");
    }

    // ── Mutability ───────────────────────────────────────

    [Fact]
    public void Coin_Dead_CanBeSetTrue()
    {
        var c = Coin.Spawn(0);
        c.Dead = true;
        Assert.True(c.Dead);
    }

    [Fact]
    public void Coin_Z_CanBeDecremented()
    {
        var c = Coin.Spawn(0);
        c.Z -= 500;
        Assert.Equal(2000, c.Z);
    }

    [Fact]
    public void Coin_Bob_CanBeIncremented()
    {
        var c   = Coin.Spawn(0);
        double initial = c.Bob;
        c.Bob += 0.5;
        Assert.Equal(initial + 0.5, c.Bob, 6);
    }
}
