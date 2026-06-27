using System.Globalization;

namespace Kantonal.Web.Formatting;

/// <summary>
/// Deterministic, culture-independent formatting for finance figures.
/// Percentages render with one decimal; CHF as whole francs; both use the
/// Swiss apostrophe group separator. Null renders as an em dash.
/// </summary>
public static class RatioFormat
{
    public const string Empty = "—";

    private static readonly NumberFormatInfo Swiss = new()
    {
        NumberGroupSeparator = "'",
        NumberDecimalSeparator = ".",
        NumberGroupSizes = new[] { 3 },
    };

    public static string Percent(decimal? value)
        => value is null ? Empty : value.Value.ToString("N1", Swiss) + " %";

    public static string Chf(decimal? value)
        => value is null ? Empty : value.Value.ToString("N0", Swiss) + " CHF";
}
