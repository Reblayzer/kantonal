using Kantonal.Web.Charting;

namespace Kantonal.Tests.Web;

public class BarChartGeometryTests
{
    [Fact]
    public void Compute_EmptyInput_ReturnsNoBars()
    {
        var layout = BarChartGeometry.Compute(Array.Empty<decimal>(), 100, 200);
        Assert.Empty(layout.Bars);
        Assert.Equal(0m, layout.MaxValue);
    }

    [Fact]
    public void Compute_ScalesHeightsToMaxAndLaysOutLeftToRight()
    {
        var layout = BarChartGeometry.Compute(new[] { 50m, 100m }, 100, 200, gap: 0);

        Assert.Equal(100m, layout.MaxValue);
        Assert.Equal(2, layout.Bars.Count);
        // bar width = (100 - 0) / 2 = 50
        Assert.Equal(50d, layout.Bars[0].Width, 3);
        Assert.Equal(0d, layout.Bars[0].X, 3);
        Assert.Equal(50d, layout.Bars[1].X, 3);
        // heights scale to max: 50/100*200=100, 100/100*200=200
        Assert.Equal(100d, layout.Bars[0].Height, 3);
        Assert.Equal(200d, layout.Bars[1].Height, 3);
        // y is the top of the bar (height - barHeight)
        Assert.Equal(100d, layout.Bars[0].Y, 3);
        Assert.Equal(0d, layout.Bars[1].Y, 3);
    }

    [Fact]
    public void Compute_AllZero_ProducesZeroHeightBarsWithoutDividingByZero()
    {
        var layout = BarChartGeometry.Compute(new[] { 0m, 0m }, 100, 200, gap: 0);
        Assert.Equal(0m, layout.MaxValue);
        Assert.All(layout.Bars, b => Assert.Equal(0d, b.Height, 3));
    }

    [Fact]
    public void Compute_NonZeroGap_SpacesBarsAndShrinksWidth()
    {
        // width 100, 3 bars, gap 5 → total gap 10, bar width (100-10)/3 = 30.
        var layout = BarChartGeometry.Compute(new[] { 10m, 20m, 30m }, 100, 200, gap: 5);

        Assert.Equal(3, layout.Bars.Count);
        Assert.All(layout.Bars, b => Assert.Equal(30d, b.Width, 3));
        Assert.Equal(0d, layout.Bars[0].X, 3);
        Assert.Equal(35d, layout.Bars[1].X, 3);  // 30 + 5
        Assert.Equal(70d, layout.Bars[2].X, 3);  // 2*(30 + 5)
    }

    [Fact]
    public void Compute_NegativeValues_ClampToZeroHeight()
    {
        var layout = BarChartGeometry.Compute(new[] { -10m, 100m }, 100, 200, gap: 0);
        Assert.Equal(0d, layout.Bars[0].Height, 3);
        Assert.Equal(200d, layout.Bars[1].Height, 3);
    }
}
