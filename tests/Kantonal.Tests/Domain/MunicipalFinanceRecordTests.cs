using Kantonal.Domain;

namespace Kantonal.Tests.Domain;

public class MunicipalFinanceRecordTests
{
    [Fact]
    public void Create_WithValidValues_ExposesProperties()
    {
        var record = new MunicipalFinanceRecord(
            BfsNumber.Create(4551), "Aadorf", 2024,
            selfFinancingRatio: 163.81m, netDebtPerCapitaChf: 1415.95m);

        Assert.Equal(4551, record.BfsNumber.Value);
        Assert.Equal("Aadorf", record.MunicipalityName);
        Assert.Equal(2024, record.Year);
        Assert.Equal(163.81m, record.SelfFinancingRatio);
        Assert.Equal(1415.95m, record.NetDebtPerCapitaChf);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void BfsNumber_Create_RejectsNonPositive(int value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => BfsNumber.Create(value));

    [Fact]
    public void Create_WithBlankName_Throws()
        => Assert.Throws<ArgumentException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(1), "  ", 2024, null, null));

    [Fact]
    public void Create_WithYearBefore1900_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(1), "X", 1899, null, null));
}
