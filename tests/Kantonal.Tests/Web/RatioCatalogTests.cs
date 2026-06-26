using Kantonal.Web.Models;

namespace Kantonal.Tests.Web;

public class RatioCatalogTests
{
    private static FinanceRow FullRow() => new(
        BfsNumber: 1, MunicipalityName: "X", Year: 2024,
        SelfFinancingRatio: 1m, SelfFinancingShare: 2m, InterestBurdenShare: 3m,
        CapitalServiceShare: 4m, InvestmentShare: 5m, GrossDebtShare: 6m,
        NetDebtPerCapitaChf: 7m, NetDebtQuotient: 8m, BalanceSheetSurplusQuotient: 9m);

    [Fact]
    public void Ratios_ExposesAllNineFieldsWithExpectedKeys()
    {
        var keys = RatioCatalog.Ratios.Select(r => r.Key).ToArray();
        Assert.Equal(new[]
        {
            "SelfFinancingRatio", "SelfFinancingShare", "InterestBurdenShare",
            "CapitalServiceShare", "InvestmentShare", "GrossDebtShare",
            "NetDebtPerCapitaChf", "NetDebtQuotient", "BalanceSheetSurplusQuotient",
        }, keys);
    }

    [Fact]
    public void Selectors_ReturnTheMatchingProperty()
    {
        var row = FullRow();
        Assert.Equal(1m, RatioCatalog.Ratios.Single(r => r.Key == "SelfFinancingRatio").Selector(row));
        Assert.Equal(7m, RatioCatalog.Ratios.Single(r => r.Key == "NetDebtPerCapitaChf").Selector(row));
        Assert.Equal(9m, RatioCatalog.Ratios.Single(r => r.Key == "BalanceSheetSurplusQuotient").Selector(row));
    }

    [Fact]
    public void Format_UsesPercentForRatiosAndChfForPerCapita()
    {
        var row = FullRow();
        Assert.Equal("1.0 %", RatioCatalog.Ratios.Single(r => r.Key == "SelfFinancingRatio").Format(row));
        Assert.Equal("7 CHF", RatioCatalog.Ratios.Single(r => r.Key == "NetDebtPerCapitaChf").Format(row));
    }
}
