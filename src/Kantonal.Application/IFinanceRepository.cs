using Kantonal.Domain;

namespace Kantonal.Application;

public interface IFinanceRepository
{
    Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct);
    Task<int> CountAsync(string? municipality, int? year, CancellationToken ct);
    Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct);
    Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct);
}
