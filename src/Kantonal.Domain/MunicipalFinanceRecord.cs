namespace Kantonal.Domain;

public sealed class MunicipalFinanceRecord
{
    // EF Core materializes through this private scalar constructor (it cannot bind a
    // value object to a ctor parameter). Domain code uses the public ctor below.
    private MunicipalFinanceRecord(
        BfsNumber bfsNumber,
        string municipalityName,
        int year,
        decimal? selfFinancingRatio,
        decimal? selfFinancingShare,
        decimal? interestBurdenShare,
        decimal? capitalServiceShare,
        decimal? investmentShare,
        decimal? grossDebtShare,
        decimal? netDebtPerCapitaChf,
        decimal? netDebtQuotient,
        decimal? balanceSheetSurplusQuotient)
    {
        if (string.IsNullOrWhiteSpace(municipalityName))
            throw new ArgumentException("Municipality name is required.", nameof(municipalityName));
        if (year < 1900)
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be 1900 or later.");

        BfsNumber = bfsNumber;
        MunicipalityName = municipalityName.Trim();
        Year = year;
        SelfFinancingRatio = selfFinancingRatio;
        SelfFinancingShare = selfFinancingShare;
        InterestBurdenShare = interestBurdenShare;
        CapitalServiceShare = capitalServiceShare;
        InvestmentShare = investmentShare;
        GrossDebtShare = grossDebtShare;
        NetDebtPerCapitaChf = netDebtPerCapitaChf;
        NetDebtQuotient = netDebtQuotient;
        BalanceSheetSurplusQuotient = balanceSheetSurplusQuotient;
    }

    public MunicipalFinanceRecord(BfsNumber bfsNumber, string municipalityName, int year, FinanceIndicators indicators)
        : this(
            bfsNumber, municipalityName, year,
            (indicators ?? throw new ArgumentNullException(nameof(indicators))).SelfFinancingRatio,
            indicators.SelfFinancingShare,
            indicators.InterestBurdenShare,
            indicators.CapitalServiceShare,
            indicators.InvestmentShare,
            indicators.GrossDebtShare,
            indicators.NetDebtPerCapitaChf,
            indicators.NetDebtQuotient,
            indicators.BalanceSheetSurplusQuotient)
    {
    }

    public BfsNumber BfsNumber { get; }
    public string MunicipalityName { get; }
    public int Year { get; }

    public decimal? SelfFinancingRatio { get; }
    public decimal? SelfFinancingShare { get; }
    public decimal? InterestBurdenShare { get; }
    public decimal? CapitalServiceShare { get; }
    public decimal? InvestmentShare { get; }
    public decimal? GrossDebtShare { get; }
    public decimal? NetDebtPerCapitaChf { get; }
    public decimal? NetDebtQuotient { get; }
    public decimal? BalanceSheetSurplusQuotient { get; }

    /// <summary>The nine ratios as a value object. Not mapped (EF Ignores it); computed from the scalar columns.</summary>
    public FinanceIndicators Indicators => new(
        SelfFinancingRatio, SelfFinancingShare, InterestBurdenShare, CapitalServiceShare,
        InvestmentShare, GrossDebtShare, NetDebtPerCapitaChf, NetDebtQuotient, BalanceSheetSurplusQuotient);
}
