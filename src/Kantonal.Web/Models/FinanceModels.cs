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

public sealed record FinanceQuery(
    string? Municipality = null,
    int? Year = null,
    string? SortBy = null,
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20);

public sealed record FilterOptions(
    IReadOnlyList<string> Municipalities,
    IReadOnlyList<int> Years);
