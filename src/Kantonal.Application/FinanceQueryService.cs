using Kantonal.Domain;

namespace Kantonal.Application;

public class FinanceQueryService
{
    private const int MaxPageSize = 100;
    private readonly IFinanceRepository _repo;

    public FinanceQueryService(IFinanceRepository repo) => _repo = repo;

    public async Task<PagedResult<FinanceRecordDto>> GetPageAsync(int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var total = await _repo.CountAsync(ct);
        var records = await _repo.GetAsync((page - 1) * pageSize, pageSize, ct);
        var items = records.Select(ToDto).ToList();

        return new PagedResult<FinanceRecordDto>(items, page, pageSize, total);
    }

    private static FinanceRecordDto ToDto(MunicipalFinanceRecord r) => new(
        r.BfsNumber.Value, r.MunicipalityName, r.Year,
        r.SelfFinancingRatio, r.SelfFinancingShare, r.InterestBurdenShare, r.CapitalServiceShare,
        r.InvestmentShare, r.GrossDebtShare, r.NetDebtPerCapitaChf, r.NetDebtQuotient, r.BalanceSheetSurplusQuotient);
}
