using System.Linq.Expressions;
using Kantonal.Application;
using Kantonal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Infrastructure;

public class EfFinanceRepository : IFinanceRepository
{
    private readonly KantonalDbContext _db;
    public EfFinanceRepository(KantonalDbContext db) => _db = db;

    public async Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct)
    {
        var q = ApplyFilters(_db.FinanceRecords.AsQueryable(), query.Municipality, query.Year);
        q = ApplySort(q, query.SortBy, query.Direction);
        return await q.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
    }

    public Task<int> CountAsync(string? municipality, int? year, CancellationToken ct)
        => ApplyFilters(_db.FinanceRecords.AsQueryable(), municipality, year).CountAsync(ct);

    public async Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct)
        => await _db.FinanceRecords.FirstOrDefaultAsync(r => r.BfsNumber == bfsNumber && r.Year == year, ct);

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

    private static IQueryable<MunicipalFinanceRecord> ApplyFilters(
        IQueryable<MunicipalFinanceRecord> q, string? municipality, int? year)
    {
        if (!string.IsNullOrWhiteSpace(municipality))
        {
            var needle = municipality.ToLower();
            q = q.Where(r => r.MunicipalityName.ToLower().Contains(needle));
        }
        if (year is not null)
            q = q.Where(r => r.Year == year);
        return q;
    }

    private static IQueryable<MunicipalFinanceRecord> ApplySort(
        IQueryable<MunicipalFinanceRecord> q, FinanceSortField sortBy, SortDirection direction)
    {
        var asc = direction == SortDirection.Asc;

        IOrderedQueryable<MunicipalFinanceRecord> ordered = sortBy switch
        {
            FinanceSortField.MunicipalityName => asc ? q.OrderBy(r => r.MunicipalityName) : q.OrderByDescending(r => r.MunicipalityName),
            FinanceSortField.Year => asc ? q.OrderBy(r => r.Year) : q.OrderByDescending(r => r.Year),
            FinanceSortField.SelfFinancingRatio => ByRatio(q, r => r.SelfFinancingRatio, asc),
            FinanceSortField.SelfFinancingShare => ByRatio(q, r => r.SelfFinancingShare, asc),
            FinanceSortField.InterestBurdenShare => ByRatio(q, r => r.InterestBurdenShare, asc),
            FinanceSortField.CapitalServiceShare => ByRatio(q, r => r.CapitalServiceShare, asc),
            FinanceSortField.InvestmentShare => ByRatio(q, r => r.InvestmentShare, asc),
            FinanceSortField.GrossDebtShare => ByRatio(q, r => r.GrossDebtShare, asc),
            FinanceSortField.NetDebtPerCapitaChf => ByRatio(q, r => r.NetDebtPerCapitaChf, asc),
            FinanceSortField.NetDebtQuotient => ByRatio(q, r => r.NetDebtQuotient, asc),
            FinanceSortField.BalanceSheetSurplusQuotient => ByRatio(q, r => r.BalanceSheetSurplusQuotient, asc),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy), sortBy, "Unhandled FinanceSortField."),
        };

        // Stable, deterministic tiebreak.
        return ordered.ThenBy(r => r.MunicipalityName).ThenBy(r => r.Year);
    }

    // Nulls always sort last (whether asc or desc) by ordering on "is null" first.
    private static IOrderedQueryable<MunicipalFinanceRecord> ByRatio(
        IQueryable<MunicipalFinanceRecord> q,
        Expression<Func<MunicipalFinanceRecord, decimal?>> key,
        bool asc)
    {
        var nullsLast = q.OrderBy(NullnessOf(key));
        return asc ? nullsLast.ThenBy(key) : nullsLast.ThenByDescending(key);
    }

    // Builds `r => <key>(r) == null` from the value key expression.
    private static Expression<Func<MunicipalFinanceRecord, bool>> NullnessOf(
        Expression<Func<MunicipalFinanceRecord, decimal?>> key)
    {
        var isNull = Expression.Equal(
            key.Body, Expression.Constant(null, typeof(decimal?)));
        return Expression.Lambda<Func<MunicipalFinanceRecord, bool>>(isNull, key.Parameters);
    }
}
