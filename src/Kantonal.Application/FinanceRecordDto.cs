namespace Kantonal.Application;

public record FinanceRecordDto(
    int BfsNumber,
    string MunicipalityName,
    int Year,
    decimal? SelfFinancingRatio,
    decimal? SelfFinancingShare,
    decimal? InterestBurdenShare,
    decimal? CapitalServiceShare,
    decimal? InvestmentShare,
    decimal? GrossDebtShare,
    decimal? NetDebtPerCapitaChf,
    decimal? NetDebtQuotient,
    decimal? BalanceSheetSurplusQuotient);
