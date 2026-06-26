namespace Kantonal.Application;

public record FinanceRecordDto(
    int BfsNumber,
    string MunicipalityName,
    int Year,
    decimal? SelfFinancingRatio,
    decimal? NetDebtPerCapitaChf);
