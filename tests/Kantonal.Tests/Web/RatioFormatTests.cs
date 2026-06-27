using Kantonal.Web.Formatting;

namespace Kantonal.Tests.Web;

public class RatioFormatTests
{
    [Theory]
    [InlineData(85.42, "85.4 %")]
    [InlineData(163.81, "163.8 %")]
    [InlineData(0, "0.0 %")]
    [InlineData(1234.5, "1'234.5 %")]
    public void Percent_FormatsToOneDecimalWithSwissGrouping(double value, string expected)
        => Assert.Equal(expected, RatioFormat.Percent((decimal)value));

    [Theory]
    [InlineData(1234, "1'234 CHF")]
    [InlineData(1234567, "1'234'567 CHF")]
    [InlineData(-684, "-684 CHF")]
    public void Chf_FormatsAsWholeChfWithSwissGrouping(double value, string expected)
        => Assert.Equal(expected, RatioFormat.Chf((decimal)value));

    [Fact]
    public void NullValues_RenderAsEmDash()
    {
        Assert.Equal("—", RatioFormat.Percent(null));
        Assert.Equal("—", RatioFormat.Chf(null));
    }
}
