using Kantonal.Application;
using Kantonal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Infrastructure;

public class EfFinanceRepository : IFinanceRepository
{
    private readonly KantonalDbContext _db;
    public EfFinanceRepository(KantonalDbContext db) => _db = db;

    public async Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct)
        => await _db.FinanceRecords
            .OrderBy(r => r.MunicipalityName).ThenBy(r => r.Year)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct) => _db.FinanceRecords.CountAsync(ct);
}
