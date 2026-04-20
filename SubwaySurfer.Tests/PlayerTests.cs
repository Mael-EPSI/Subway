using SubwaySurfer.Entities;
using Xunit;

namespace SubwaySurfer.Tests;

public class PlayerTests
{
    // ── MoveLeft ─────────────────────────────────────────

    [Fact]
    public void MoveLeft_DecreasesLane()
    {
        var p = new Player();
        p.MoveLeft();
        Assert.Equal(-1, p.Lane);
    }

    [Fact]
    public void MoveLeft_CannotGoBelowMinusOne()
    {
        var p = new Player();
        p.MoveLeft(); p.MoveLeft(); p.MoveLeft();
        Assert.Equal(-1, p.Lane);
    }

    [Fact]
    public void MoveLeft_FromRight_ReachesCenter()
    {
        var p = new Player();
        p.MoveRight();   // lane = 1
        p.MoveLeft();    // lane = 0
        Assert.Equal(0, p.Lane);
    }

    // ── MoveRight ────────────────────────────────────────

    [Fact]
    public void MoveRight_IncreasesLane()
    {
        var p = new Player();
        p.MoveRight();
        Assert.Equal(1, p.Lane);
    }

    [Fact]
    public void MoveRight_CannotGoAboveOne()
    {
        var p = new Player();
        p.MoveRight(); p.MoveRight(); p.MoveRight();
        Assert.Equal(1, p.Lane);
    }

    // ── Jump ─────────────────────────────────────────────

    [Fact]
    public void Jump_SetsIsJumping_True()
    {
        var p = new Player();
        p.Jump();
        Assert.True(p.IsJumping);
    }

    [Fact]
    public void Jump_DoesNothing_WhenAlreadyJumping()
    {
        var p = new Player();
        p.Jump();
        p.Update(0.1, 500); // advance JumpT so it's mid-jump
        double jumpT = p.JumpT;

        p.Jump(); // second call — should NOT reset JumpT to 0
        Assert.True(p.IsJumping);
        Assert.True(p.JumpT > 0, "JumpT should not have been reset by a second Jump()");
    }

    [Fact]
    public void Jump_ResetsJumpT_ToZero_OnFirstCall()
    {
        var p = new Player();
        p.Jump();
        Assert.Equal(0, p.JumpT);
    }

    // ── Update — jump physics ────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.13)]
    [InlineData(0.25)]
    public void Update_JumpY_IsZero_WhenNotJumping(double dt)
    {
        var p = new Player();
        p.Update(dt, 500);
        Assert.Equal(0, p.JumpY);
    }

    [Fact]
    public void Update_JumpY_PositiveMidArc()
    {
        var p = new Player();
        p.Jump();
        p.Update(0.25, 500); // mid-way through 0.50s jump
        Assert.True(p.JumpY > 0, "JumpY should be positive mid-arc");
    }

    [Fact]
    public void Update_JumpY_ReturnsToZero_WhenJumpComplete()
    {
        var p = new Player();
        p.Jump();
        // Advance past JumpDur (0.50 s) in small steps
        for (int i = 0; i < 60; i++) p.Update(0.02, 500);
        Assert.Equal(0, p.JumpY);
        Assert.False(p.IsJumping);
    }

    [Fact]
    public void Update_JumpT_Increases_WhileJumping()
    {
        var p = new Player();
        p.Jump();
        p.Update(0.1, 500);
        Assert.True(p.JumpT > 0);
    }

    [Fact]
    public void Update_IsJumping_FalseAfterFullArc()
    {
        var p = new Player();
        p.Jump();
        for (int i = 0; i < 60; i++) p.Update(0.02, 500);
        Assert.False(p.IsJumping);
    }

    // ── Update — animation ───────────────────────────────

    [Fact]
    public void Update_LegAnim_Increases_EachFrame()
    {
        var p = new Player();
        double before = p.LegAnim;
        p.Update(0.016, 500);
        Assert.True(p.LegAnim > before);
    }

    [Fact]
    public void Update_RunFrame_AlternatesEvery125ms()
    {
        var p = new Player();
        Assert.Equal(0, p.RunFrame);

        p.Update(0.13, 500); // just past threshold
        Assert.Equal(1, p.RunFrame);

        p.Update(0.13, 500);
        Assert.Equal(0, p.RunFrame);
    }

    [Fact]
    public void Update_RunFrame_DoesNotFlip_BeforeThreshold()
    {
        var p = new Player();
        p.Update(0.05, 500); // below 0.125 s
        Assert.Equal(0, p.RunFrame);
    }

    // ── Reset ────────────────────────────────────────────

    [Fact]
    public void Reset_RestoresAllDefaults()
    {
        var p = new Player();
        p.MoveRight();
        p.Jump();
        for (int i = 0; i < 10; i++) p.Update(0.016, 500);

        p.Reset();

        Assert.Equal(0, p.Lane);
        Assert.Equal(0, p.Vx);
        Assert.False(p.IsJumping);
        Assert.Equal(0, p.JumpT);
        Assert.Equal(0, p.JumpY);
        Assert.Equal(0, p.LegAnim);
        Assert.Equal(0, p.RunFrame);
    }
}
