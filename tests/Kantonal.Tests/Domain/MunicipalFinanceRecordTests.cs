using Kantonal.Domain;

namespace Kantonal.Tests.Domain;

public class MunicipalFinanceRecordTests
{
    private static FinanceIndicators SampleIndicators() => new(
        SelfFinancingRatio: 163.81m, SelfFinancingShare: 20.20m, InterestBurdenShare: 0.63m,
        CapitalServiceShare: 6.81m, InvestmentShare: 14.01m, GrossDebtShare: 141.04m,
        NetDebtPerCapitaChf: 1415.95m, NetDebtQuotient: 105.81m, BalanceSheetSurplusQuotient: 128.37m);

    [Fact]
    public void Constructor_ExposesIndicatorsAndScalars()
    {
        var record = new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, SampleIndicators());

        Assert.Equal(4551, record.BfsNumber.Value);
        Assert.Equal("Aadorf", record.MunicipalityName);
        Assert.Equal(2024, record.Year);
        Assert.Equal(163.81m, record.SelfFinancingRatio);
        Assert.Equal(128.37m, record.BalanceSheetSurplusQuotient);
        // computed grouping round-trips back to an equal value object
        Assert.Equal(SampleIndicators(), record.Indicators);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankName(string name)
        => Assert.Throws<ArgumentException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(4551), name, 2024, SampleIndicators()));

    [Fact]
    public void Constructor_RejectsYearBefore1900()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 1899, SampleIndicators()));

    [Fact]
    public void Indicators_HasValueEquality()
        => Assert.Equal(SampleIndicators(), SampleIndicators());
}
