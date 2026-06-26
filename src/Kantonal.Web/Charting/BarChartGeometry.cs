namespace Kantonal.Web.Charting;

public readonly record struct Bar(double X, double Y, double Width, double Height);

public sealed record BarChartLayout(IReadOnlyList<Bar> Bars, decimal MaxValue);

/// <summary>
/// Pure layout for a simple vertical bar chart. Heights scale from a zero
/// baseline to the maximum value; negative values clamp to zero height
/// (the chart is fed top-N-descending data, so charted values are positive).
/// </summary>
public static class BarChartGeometry
{
    public static BarChartLayout Compute(IReadOnlyList<decimal> values, double width, double height, double gap = 4)
    {
        if (values.Count == 0)
            return new BarChartLayout(Array.Empty<Bar>(), 0m);

        var max = values.Max();
        var barWidth = (width - gap * (values.Count - 1)) / values.Count;

        var bars = new List<Bar>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var barHeight = max > 0m ? Math.Max(0d, (double)(values[i] / max) * height) : 0d;
            var x = i * (barWidth + gap);
            bars.Add(new Bar(x, height - barHeight, barWidth, barHeight));
        }

        return new BarChartLayout(bars, max);
    }
}
