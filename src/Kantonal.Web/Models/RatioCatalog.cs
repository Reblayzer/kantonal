using Kantonal.Web.Formatting;

namespace Kantonal.Web.Models;

public enum RatioUnit { Percent, Chf }

/// <summary>One HRM2 ratio: its API sort key, display label, unit, and accessor.</summary>
public sealed record RatioInfo(string Key, string Label, RatioUnit Unit, Func<FinanceRow, decimal?> Selector)
{
    public string Format(FinanceRow row) => Unit == RatioUnit.Percent
        ? RatioFormat.Percent(Selector(row))
        : RatioFormat.Chf(Selector(row));
}

/// <summary>Single source of truth for the nine ratios. Keys match FinanceSortField names.</summary>
public static class RatioCatalog
{
    public static readonly IReadOnlyList<RatioInfo> Ratios = new[]
    {
        new RatioInfo("SelfFinancingRatio", "Self-financing ratio", RatioUnit.Percent, r => r.SelfFinancingRatio),
        new RatioInfo("SelfFinancingShare", "Self-financing share", RatioUnit.Percent, r => r.SelfFinancingShare),
        new RatioInfo("InterestBurdenShare", "Interest burden share", RatioUnit.Percent, r => r.InterestBurdenShare),
        new RatioInfo("CapitalServiceShare", "Capital service share", RatioUnit.Percent, r => r.CapitalServiceShare),
        new RatioInfo("InvestmentShare", "Investment share", RatioUnit.Percent, r => r.InvestmentShare),
        new RatioInfo("GrossDebtShare", "Gross debt share", RatioUnit.Percent, r => r.GrossDebtShare),
        new RatioInfo("NetDebtPerCapitaChf", "Net debt/capita", RatioUnit.Chf, r => r.NetDebtPerCapitaChf),
        new RatioInfo("NetDebtQuotient", "Net debt quotient", RatioUnit.Percent, r => r.NetDebtQuotient),
        new RatioInfo("BalanceSheetSurplusQuotient", "Balance-sheet surplus quotient", RatioUnit.Percent, r => r.BalanceSheetSurplusQuotient),
    };
}
