namespace Kantonal.Application;

public enum SortDirection { Asc, Desc }

public enum FinanceSortField
{
    MunicipalityName,
    Year,
    SelfFinancingRatio,
    SelfFinancingShare,
    InterestBurdenShare,
    CapitalServiceShare,
    InvestmentShare,
    GrossDebtShare,
    NetDebtPerCapitaChf,
    NetDebtQuotient,
    BalanceSheetSurplusQuotient,
}

public sealed record FinanceQuery(
    string? Municipality,
    int? Year,
    FinanceSortField SortBy,
    SortDirection Direction,
    int Skip,
    int Take);
