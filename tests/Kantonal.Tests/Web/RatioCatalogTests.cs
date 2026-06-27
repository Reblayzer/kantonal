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
        // FullRow assigns each property a distinct value 1..9 in catalog order,
        // so the i-th ratio's selector must return i+1.
        var row = FullRow();
        var expected = new[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m };
        for (var i = 0; i < RatioCatalog.Ratios.Count; i++)
            Assert.Equal(expected[i], RatioCatalog.Ratios[i].Selector(row));
    }

    [Fact]
    public void Units_ExactlyOneChfTheRestPercent()
    {
        var chf = RatioCatalog.Ratios.Where(r => r.Unit == RatioUnit.Chf).ToArray();
        Assert.Equal("NetDebtPerCapitaChf", Assert.Single(chf).Key);
        Assert.All(
            RatioCatalog.Ratios.Where(r => r.Key != "NetDebtPerCapitaChf"),
            r => Assert.Equal(RatioUnit.Percent, r.Unit));
    }

    [Fact]
    public void Format_UsesPercentForRatiosAndChfForPerCapita()
    {
        var row = FullRow();
        Assert.Equal("1.0 %", RatioCatalog.Ratios.Single(r => r.Key == "SelfFinancingRatio").Format(row));
        Assert.Equal("7 CHF", RatioCatalog.Ratios.Single(r => r.Key == "NetDebtPerCapitaChf").Format(row));
    }
}
