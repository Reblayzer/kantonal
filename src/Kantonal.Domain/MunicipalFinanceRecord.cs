namespace Kantonal.Domain;

public sealed class MunicipalFinanceRecord
{
    public MunicipalFinanceRecord(
        BfsNumber bfsNumber,
        string municipalityName,
        int year,
        decimal? selfFinancingRatio,
        decimal? netDebtPerCapitaChf)
    {
        if (string.IsNullOrWhiteSpace(municipalityName))
            throw new ArgumentException("Municipality name is required.", nameof(municipalityName));
        if (year < 1900)
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be 1900 or later.");

        BfsNumber = bfsNumber;
        MunicipalityName = municipalityName.Trim();
        Year = year;
        SelfFinancingRatio = selfFinancingRatio;
        NetDebtPerCapitaChf = netDebtPerCapitaChf;
    }

    public BfsNumber BfsNumber { get; }
    public string MunicipalityName { get; }
    public int Year { get; }
    public decimal? SelfFinancingRatio { get; }
    public decimal? NetDebtPerCapitaChf { get; }
}
