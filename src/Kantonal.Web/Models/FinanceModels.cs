namespace Kantonal.Web.Models;

public record FinanceRow(
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

public record FinancePage(IReadOnlyList<FinanceRow> Items, int Page, int PageSize, int Total);
