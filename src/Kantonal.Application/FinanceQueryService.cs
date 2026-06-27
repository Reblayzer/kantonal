using Kantonal.Application.Errors;
using Kantonal.Domain;

namespace Kantonal.Application;

public sealed class FinanceQueryService
{
    private const int MaxPageSize = 100;
    private readonly IFinanceRepository _repo;

    public FinanceQueryService(IFinanceRepository repo) => _repo = repo;

    public async Task<PagedResult<FinanceRecordDto>> GetPageAsync(FinanceListRequest request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var sortBy = ParseSortField(request.SortBy);
        var direction = ParseDirection(request.SortDir);

        var query = new FinanceQuery(request.Municipality, request.Year, sortBy, direction,
            (page - 1) * pageSize, pageSize);

        var total = await _repo.CountAsync(request.Municipality, request.Year, ct);
        var records = await _repo.QueryAsync(query, ct);

        return new PagedResult<FinanceRecordDto>(records.Select(ToDto).ToList(), page, pageSize, total);
    }

    public Task<FinanceFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
        => _repo.GetFilterOptionsAsync(ct);

    public async Task<FinanceRecordDto> GetByKeyAsync(int bfsNumber, int year, CancellationToken ct)
    {
        if (bfsNumber <= 0)
            throw new ValidationException("invalid_bfs", $"BFS number must be positive; got {bfsNumber}.");

        var record = await _repo.GetByKeyAsync(BfsNumber.Create(bfsNumber), year, ct);
        if (record is null)
            throw new NotFoundException("finance_record_not_found",
                $"No finance record for BFS {bfsNumber}, year {year}.");

        return ToDto(record);
    }

    private static FinanceSortField ParseSortField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FinanceSortField.MunicipalityName;
        if (Enum.TryParse<FinanceSortField>(value, ignoreCase: true, out var field) && Enum.IsDefined(field))
            return field;
        throw new ValidationException("invalid_sort_field", $"Unknown sortBy value '{value}'.");
    }

    private static SortDirection ParseDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SortDirection.Asc;
        if (Enum.TryParse<SortDirection>(value, ignoreCase: true, out var dir) && Enum.IsDefined(dir))
            return dir;
        throw new ValidationException("invalid_sort_dir", $"Unknown sortDir value '{value}'.");
    }

    private static FinanceRecordDto ToDto(MunicipalFinanceRecord r) => new(
        r.BfsNumber.Value, r.MunicipalityName, r.Year,
        r.SelfFinancingRatio, r.SelfFinancingShare, r.InterestBurdenShare, r.CapitalServiceShare,
        r.InvestmentShare, r.GrossDebtShare, r.NetDebtPerCapitaChf, r.NetDebtQuotient, r.BalanceSheetSurplusQuotient);
}
