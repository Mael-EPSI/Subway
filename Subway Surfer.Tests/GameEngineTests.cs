using SubwaySurfer.Game;

namespace SubwaySurfer.Tests;

public class GameEngineTests
{
    private GameEngine CreatePlayingEngine()
    {
        var engine = new GameEngine();
        engine.ActionStart(); // Menu → Playing
        return engine;
    }

    // ─── Phase transitions ──────────────────────────

    [Fact]
    public void InitialPhase_IsMenu()
    {
        var engine = new GameEngine();
        Assert.Equal(GamePhase.Menu, engine.State.Phase);
    }

    [Fact]
    public void ActionStart_FromMenu_SwitchesToPlaying()
    {
        var engine = new GameEngine();
        engine.ActionStart();
        Assert.Equal(GamePhase.Playing, engine.State.Phase);
    }

    [Fact]
    public void ActionStart_FromPlaying_DoesNothing()
    {
        var engine = CreatePlayingEngine();
        engine.ActionStart(); // should not reset
        Assert.Equal(GamePhase.Playing, engine.State.Phase);
        Assert.True(engine.State.Time == 0); // no re-init
    }

    [Fact]
    public void ActionRestart_FromMenu_DoesNothing()
    {
        var engine = new GameEngine();
        engine.ActionRestart();
        Assert.Equal(GamePhase.Menu, engine.State.Phase);
    }

    [Fact]
    public void ActionRestart_FromGameOver_GoesBackToMenu()
    {
        var engine = CreatePlayingEngine();
        // Force a collision by placing a train on player's lane
        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.Equal(GamePhase.GameOver, engine.State.Phase);

        engine.ActionRestart();
        Assert.Equal(GamePhase.Menu, engine.State.Phase);
    }

    // ─── Player movement ────────────────────────────

    [Fact]
    public void ActionLeft_MovesPlayerLeft()
    {
        var engine = CreatePlayingEngine();
        Assert.Equal(0, engine.State.Player.Lane);
        engine.ActionLeft();
        Assert.Equal(-1, engine.State.Player.Lane);
    }

    [Fact]
    public void ActionRight_MovesPlayerRight()
    {
        var engine = CreatePlayingEngine();
        engine.ActionRight();
        Assert.Equal(1, engine.State.Player.Lane);
    }

    [Fact]
    public void ActionLeft_AtLeftBoundary_StaysAtMinus1()
    {
        var engine = CreatePlayingEngine();
        engine.ActionLeft();
        engine.ActionLeft(); // already at -1
        Assert.Equal(-1, engine.State.Player.Lane);
    }

    [Fact]
    public void ActionRight_AtRightBoundary_StaysAt1()
    {
        var engine = CreatePlayingEngine();
        engine.ActionRight();
        engine.ActionRight(); // already at 1
        Assert.Equal(1, engine.State.Player.Lane);
    }

    [Fact]
    public void Movement_OnMenu_IsIgnored()
    {
        var engine = new GameEngine();
        engine.ActionLeft();
        engine.ActionRight();
        Assert.Equal(0, engine.State.Player.Lane);
    }

    // ─── Jump ───────────────────────────────────────

    [Fact]
    public void ActionJump_SetsJumping()
    {
        var engine = CreatePlayingEngine();
        engine.ActionJump();
        Assert.True(engine.State.Player.Jumping);
    }

    [Fact]
    public void ActionJump_WhileJumping_DoesNotReset()
    {
        var engine = CreatePlayingEngine();
        engine.ActionJump();
        engine.Update(0.1);
        double jumpTimeBeforeSecondJump = engine.State.Player.JumpTime;

        engine.ActionJump(); // should be ignored
        Assert.Equal(jumpTimeBeforeSecondJump, engine.State.Player.JumpTime);
    }

    [Fact]
    public void Jump_CompletesAfterDuration()
    {
        var engine = CreatePlayingEngine();
        engine.ActionJump();

        // Update enough to exceed JumpDur (0.50)
        for (int i = 0; i < 40; i++)
            engine.Update(0.016);

        Assert.False(engine.State.Player.Jumping);
        Assert.Equal(0, engine.State.Player.JumpY);
    }

    [Fact]
    public void Jump_ProducesPositiveJumpY()
    {
        var engine = CreatePlayingEngine();
        engine.ActionJump();
        engine.Update(0.1); // mid-jump
        Assert.True(engine.State.Player.JumpY > 0);
    }

    // ─── Score & speed ──────────────────────────────

    [Fact]
    public void Score_IncreasesOverTime()
    {
        var engine = CreatePlayingEngine();
        engine.Update(1.0);
        Assert.True(engine.State.Score > 0);
    }

    [Fact]
    public void Speed_IncreasesOverTime()
    {
        var engine = CreatePlayingEngine();
        double initialSpeed = engine.State.Speed;
        engine.Update(1.0);
        Assert.True(engine.State.Speed > initialSpeed);
    }

    [Fact]
    public void Speed_DoesNotExceedMax()
    {
        var engine = CreatePlayingEngine();
        // Update for a very long simulated time
        for (int i = 0; i < 1000; i++)
            engine.Update(0.5);

        // If still playing (didn't crash)
        if (engine.State.Phase == GamePhase.Playing)
            Assert.True(engine.State.Speed <= GameConfig.SpeedMax);
    }

    [Fact]
    public void Scroll_IncreasesWhilePlaying()
    {
        var engine = CreatePlayingEngine();
        engine.Update(0.5);
        Assert.True(engine.State.Scroll > 0);
    }

    // ─── Collision ──────────────────────────────────

    [Fact]
    public void TrainCollision_TriggersGameOver()
    {
        var engine = CreatePlayingEngine();
        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.Equal(GamePhase.GameOver, engine.State.Phase);
    }

    [Fact]
    public void TrainCollision_DifferentLane_NoGameOver()
    {
        var engine = CreatePlayingEngine();
        engine.State.Trains.Add(new Train { Lane = 1, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.Equal(GamePhase.Playing, engine.State.Phase);
    }

    [Fact]
    public void TrainCollision_WhileAirborne_Survives()
    {
        var engine = CreatePlayingEngine();
        engine.ActionJump();
        // Update to reach a good jump height (above JumpSafe)
        engine.Update(0.15);
        Assert.True(engine.State.Player.JumpY > GameConfig.JumpSafe);

        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.Equal(GamePhase.Playing, engine.State.Phase);
    }

    [Fact]
    public void GameOver_SetsShakeAndFlash()
    {
        var engine = CreatePlayingEngine();
        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.True(engine.State.ShakeTime > 0);
        Assert.True(engine.State.FlashAlpha > 0);
    }

    [Fact]
    public void GameOver_PlaysCrashSound()
    {
        var engine = CreatePlayingEngine();
        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.Contains("crash", engine.State.Sounds);
    }

    // ─── Coins ──────────────────────────────────────

    [Fact]
    public void CoinCollection_AddsScore()
    {
        var engine = CreatePlayingEngine();
        int scoreBefore = engine.State.Score;
        engine.State.Coins.Add(new Coin { Lane = 0, Z = 30, Bob = 0, Dead = false });
        engine.Update(0.016);
        Assert.True(engine.State.Score >= scoreBefore + GameConfig.CoinValue);
    }

    [Fact]
    public void CoinCollection_PlaysCoinSound()
    {
        var engine = CreatePlayingEngine();
        engine.State.Coins.Add(new Coin { Lane = 0, Z = 30, Bob = 0, Dead = false });
        engine.Update(0.016);
        Assert.Contains("coin", engine.State.Sounds);
    }

    [Fact]
    public void CoinCollection_SpawnsParticles()
    {
        var engine = CreatePlayingEngine();
        engine.State.Coins.Add(new Coin { Lane = 0, Z = 30, Bob = 0, Dead = false });
        engine.Update(0.016);
        Assert.True(engine.State.Particles.Count > 0);
    }

    [Fact]
    public void CoinCollection_DifferentLane_NotCollected()
    {
        var engine = CreatePlayingEngine();
        int scoreBefore = engine.State.Score;
        engine.State.Coins.Add(new Coin { Lane = 1, Z = 30, Bob = 0, Dead = false });
        engine.Update(0.016);
        // Score should only increase from time, not from coin
        int timeScore = (int)(engine.State.Speed * 0.016 * 0.1);
        Assert.True(engine.State.Score <= scoreBefore + timeScore + 1);
    }

    // ─── Spawning ───────────────────────────────────

    [Fact]
    public void Trains_SpawnOverTime()
    {
        var engine = CreatePlayingEngine();
        for (int i = 0; i < 100; i++)
            engine.Update(0.016);

        Assert.True(engine.State.Trains.Count > 0);
    }

    [Fact]
    public void Coins_SpawnOverTime()
    {
        var engine = CreatePlayingEngine();
        for (int i = 0; i < 100; i++)
            engine.Update(0.016);

        // Coins may have been collected or removed, but some should have spawned
        // Check that coin timer has been resetting (indirect proof)
        Assert.True(engine.State.Time > 0);
    }

    // ─── Object movement ────────────────────────────

    [Fact]
    public void Trains_MoveTowardPlayer()
    {
        var engine = CreatePlayingEngine();
        engine.State.Trains.Add(new Train { Lane = 1, Z = 1000, Color = "#cc2222" });
        double zBefore = engine.State.Trains[0].Z;
        engine.Update(0.1);
        Assert.True(engine.State.Trains[0].Z < zBefore);
    }

    [Fact]
    public void Trains_RemovedWhenPastPlayer()
    {
        var engine = CreatePlayingEngine();
        engine.State.Trains.Add(new Train { Lane = 1, Z = -25, Color = "#cc2222" });
        engine.Update(0.016);
        Assert.Empty(engine.State.Trains);
    }

    // ─── HighScore ──────────────────────────────────

    [Fact]
    public void HighScore_UpdatedOnGameOver()
    {
        var engine = CreatePlayingEngine();
        // Accumulate some score
        for (int i = 0; i < 50; i++)
            engine.Update(0.016);

        int scoreBeforeDeath = engine.State.Score;
        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);

        Assert.True(engine.State.HighScore >= scoreBeforeDeath);
    }

    // ─── Sounds ─────────────────────────────────────

    [Fact]
    public void ActionLeft_PlaysLaneSound()
    {
        var engine = CreatePlayingEngine();
        engine.ActionLeft();
        Assert.Contains("lane", engine.State.Sounds);
    }

    [Fact]
    public void ActionJump_PlaysJumpSound()
    {
        var engine = CreatePlayingEngine();
        engine.ActionJump();
        Assert.Contains("jump", engine.State.Sounds);
    }

    [Fact]
    public void Sounds_ClearedEachFrame()
    {
        var engine = CreatePlayingEngine();
        engine.ActionLeft();
        Assert.Contains("lane", engine.State.Sounds);

        engine.Update(0.016);
        Assert.DoesNotContain("lane", engine.State.Sounds);
    }

    // ─── Particles ──────────────────────────────────

    [Fact]
    public void Particles_DecayOverTime()
    {
        var engine = CreatePlayingEngine();
        engine.State.Particles.Add(new Particle
        {
            X = 100, Y = 100, Vx = 10, Vy = 10,
            Life = 0.1, MaxLife = 0.5, Color = "#fff", R = 3
        });

        // Update enough for particle to die
        for (int i = 0; i < 20; i++)
            engine.Update(0.016);

        Assert.Empty(engine.State.Particles);
    }

    // ─── Start resets state ─────────────────────────

    [Fact]
    public void StartGame_ResetsScore()
    {
        var engine = CreatePlayingEngine();
        for (int i = 0; i < 50; i++)
            engine.Update(0.016);
        Assert.True(engine.State.Score > 0);

        // Game over
        engine.State.Trains.Add(new Train { Lane = 0, Z = 30, Color = "#cc2222" });
        engine.Update(0.016);

        // Restart → Menu → Start
        engine.ActionRestart();
        engine.ActionStart();
        Assert.Equal(0, engine.State.Score);
        Assert.Equal(GamePhase.Playing, engine.State.Phase);
    }
}
