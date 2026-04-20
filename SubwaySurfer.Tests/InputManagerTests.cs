using System.Windows.Input;
using Xunit;
using InputManager = SubwaySurfer.Engine.InputManager;

namespace SubwaySurfer.Tests;

public class InputManagerTests
{
    // ── ConsumeLeft ──────────────────────────────────────

    [Fact]
    public void ConsumeLeft_TrueAfterKeyDown_Left()
    {
        var im = new InputManager();
        im.KeyDown(Key.Left);
        Assert.True(im.ConsumeLeft());
    }

    [Fact]
    public void ConsumeLeft_FalseOnSecondConsume()
    {
        var im = new InputManager();
        im.KeyDown(Key.Left);
        im.ConsumeLeft();
        Assert.False(im.ConsumeLeft());
    }

    [Fact]
    public void ConsumeLeft_FalseAfterKeyUp()
    {
        var im = new InputManager();
        im.KeyDown(Key.Left);
        im.KeyUp(Key.Left);
        Assert.False(im.ConsumeLeft());
    }

    // ── ConsumeRight ─────────────────────────────────────

    [Fact]
    public void ConsumeRight_TrueAfterKeyDown_Right()
    {
        var im = new InputManager();
        im.KeyDown(Key.Right);
        Assert.True(im.ConsumeRight());
    }

    [Fact]
    public void ConsumeRight_FalseOnSecondConsume()
    {
        var im = new InputManager();
        im.KeyDown(Key.Right);
        im.ConsumeRight();
        Assert.False(im.ConsumeRight());
    }

    [Fact]
    public void ConsumeRight_FalseAfterKeyUp()
    {
        var im = new InputManager();
        im.KeyDown(Key.Right);
        im.KeyUp(Key.Right);
        Assert.False(im.ConsumeRight());
    }

    // ── ConsumeJump ──────────────────────────────────────

    [Fact]
    public void ConsumeJump_TrueAfterKeyDown_Space()
    {
        var im = new InputManager();
        im.KeyDown(Key.Space);
        Assert.True(im.ConsumeJump());
    }

    [Fact]
    public void ConsumeJump_FalseOnSecondConsume()
    {
        var im = new InputManager();
        im.KeyDown(Key.Space);
        im.ConsumeJump();
        Assert.False(im.ConsumeJump());
    }

    [Fact]
    public void ConsumeJump_FalseAfterKeyUp_Space()
    {
        var im = new InputManager();
        im.KeyDown(Key.Space);
        im.KeyUp(Key.Space);
        Assert.False(im.ConsumeJump());
    }

    // ── Edge trigger: holding key only fires once ─────────

    [Fact]
    public void MultipleKeyDown_SameKey_TriggersOnce_Left()
    {
        var im = new InputManager();
        im.KeyDown(Key.Left);
        im.KeyDown(Key.Left);
        im.KeyDown(Key.Left);
        Assert.True(im.ConsumeLeft());
        Assert.False(im.ConsumeLeft()); // consumed on first call
    }

    [Fact]
    public void MultipleKeyDown_SameKey_TriggersOnce_Jump()
    {
        var im = new InputManager();
        im.KeyDown(Key.Space);
        im.KeyDown(Key.Space); // held — edge-trigger: _jumpConsumed prevents re-arm
        Assert.True(im.ConsumeJump());
        Assert.False(im.ConsumeJump());
    }

    // ── Key aliases ──────────────────────────────────────

    [Fact]
    public void KeyDown_A_SameAs_Left()
    {
        var im = new InputManager();
        im.KeyDown(Key.A);
        Assert.True(im.ConsumeLeft());
    }

    [Fact]
    public void KeyDown_D_SameAs_Right()
    {
        var im = new InputManager();
        im.KeyDown(Key.D);
        Assert.True(im.ConsumeRight());
    }

    [Fact]
    public void KeyDown_W_SameAs_Space()
    {
        var im = new InputManager();
        im.KeyDown(Key.W);
        Assert.True(im.ConsumeJump());
    }

    [Fact]
    public void KeyDown_Up_SameAs_Space()
    {
        var im = new InputManager();
        im.KeyDown(Key.Up);
        Assert.True(im.ConsumeJump());
    }

    // ── KeyUp releases alias ─────────────────────────────

    [Fact]
    public void KeyUp_A_ReleasesLeft()
    {
        var im = new InputManager();
        im.KeyDown(Key.A);
        im.KeyUp(Key.A);
        Assert.False(im.ConsumeLeft());
    }

    [Fact]
    public void KeyUp_W_ReleasesJump_AndReArms()
    {
        var im = new InputManager();
        im.KeyDown(Key.W);
        im.ConsumeJump();
        im.KeyUp(Key.W);

        // After key-up the edge-trigger is reset, so a new press should fire
        im.KeyDown(Key.W);
        Assert.True(im.ConsumeJump());
    }
}
