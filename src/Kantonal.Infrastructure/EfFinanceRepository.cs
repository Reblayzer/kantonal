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

    public async Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return 0;

        // Load existing rows once, then partition in memory — no query inside the loop.
        var existing = await _db.FinanceRecords.ToListAsync(ct);
        var byKey = existing.ToDictionary(r => (r.BfsNumber, r.Year));

        foreach (var record in records)
        {
            if (byKey.TryGetValue((record.BfsNumber, record.Year), out var current))
                _db.Entry(current).CurrentValues.SetValues(record);
            else
                _db.FinanceRecords.Add(record);
        }

        await _db.SaveChangesAsync(ct);
        return records.Count;
    }
}
