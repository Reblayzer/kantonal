namespace Kantonal.Web.Models;

public record FinanceRow(
    int BfsNumber,
    string MunicipalityName,
    int Year,
    decimal? SelfFinancingRatio,
    decimal? NetDebtPerCapitaChf);

public record FinancePage(IReadOnlyList<FinanceRow> Items, int Page, int PageSize, int Total);
