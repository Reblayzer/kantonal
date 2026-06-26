using Kantonal.Domain;

namespace Kantonal.Application;

public interface IFinanceRepository
{
    Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
}
