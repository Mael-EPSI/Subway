using SubwaySurfer.Engine;
using Xunit;

namespace SubwaySurfer.Tests;

public class PerspectiveCameraTests
{
    // Canvas 800×600, vpX = 400, vpY = 210
    private static PerspectiveCamera Cam() => new(800, 600);

    // ── Project — X axis ─────────────────────────────────

    [Fact]
    public void Project_ZeroWorldX_AlwaysMapsToVpX()
    {
        var cam = Cam();
        var pt  = cam.Project(0, 500);
        Assert.Equal(cam.VpX, pt.X);
    }

    [Fact]
    public void Project_ZeroWorldX_LargeZ_StillVpX()
    {
        var cam = Cam();
        var pt  = cam.Project(0, 2000);
        Assert.Equal(cam.VpX, pt.X, 3);
    }

    [Fact]
    public void Project_PositiveWorldX_ScreenX_GreaterThan_VpX()
    {
        var cam = Cam();
        var pt  = cam.Project(100, 500);
        Assert.True(pt.X > cam.VpX);
    }

    [Fact]
    public void Project_NegativeWorldX_ScreenX_LessThan_VpX()
    {
        var cam = Cam();
        var pt  = cam.Project(-100, 500);
        Assert.True(pt.X < cam.VpX);
    }

    // ── Project — scale formula ───────────────────────────

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void Project_Scale_MatchesFormula(double z)
    {
        const double Fov = 250.0;
        var cam      = Cam();
        var pt       = cam.Project(0, z);
        double expected = Fov / (Fov + z);
        Assert.Equal(expected, pt.Scale, 6);
    }

    [Fact]
    public void Scale_IsLarger_WhenObjectClose()
    {
        var cam  = Cam();
        var near = cam.Project(0, 50);
        var far  = cam.Project(0, 1000);
        Assert.True(near.Scale > far.Scale);
    }

    [Fact]
    public void Scale_IsSmallerAsZ_Increases()
    {
        var cam = Cam();
        double prev = 1.0;
        foreach (double z in new[] { 100.0, 500.0, 1000.0, 2000.0 })
        {
            double s = cam.Project(0, z).Scale;
            Assert.True(s < prev, $"Scale should decrease at z={z}");
            prev = s;
        }
    }

    // ── DepthAlpha ────────────────────────────────────────

    [Fact]
    public void DepthAlpha_Zero_ReturnsOne()
    {
        Assert.Equal(1.0, Cam().DepthAlpha(0), 6);
    }

    [Fact]
    public void DepthAlpha_OverTwoThousand_Returns0Point25()
    {
        Assert.Equal(0.25, Cam().DepthAlpha(2001), 6);
    }

    [Fact]
    public void DepthAlpha_ExactlyTwoThousand_Returns0Point25()
    {
        // Formula: 0.25 + 0.75*(1 - 2000/2000) = 0.25
        Assert.Equal(0.25, Cam().DepthAlpha(2000), 6);
    }

    [Fact]
    public void DepthAlpha_Mid_IsBetween025And1()
    {
        double a = Cam().DepthAlpha(1000);
        Assert.True(a > 0.25 && a < 1.0,
            $"DepthAlpha(1000) should be in (0.25, 1.0), got {a}");
    }

    [Fact]
    public void DepthAlpha_DecreasesAsZIncreases()
    {
        var cam = Cam();
        Assert.True(cam.DepthAlpha(0) > cam.DepthAlpha(500));
        Assert.True(cam.DepthAlpha(500) > cam.DepthAlpha(1500));
    }

    // ── Edge: Z <= 0 ─────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void Project_ZeroOrNegativeZ_DoesNotThrow(double z)
    {
        var cam = Cam();
        var ex  = Record.Exception(() => cam.Project(0, z));
        Assert.Null(ex);
    }

    [Fact]
    public void Project_NegativeZ_ClampsToNearPlane()
    {
        var cam  = Cam();
        var ptN  = cam.Project(0, -100);
        var ptC  = cam.Project(0, 0.001);
        // Both should be equivalent since negative Z clamps to 0.001
        Assert.Equal(ptC.Scale, ptN.Scale, 4);
    }
}
