using Kantonal.Application;
using Kantonal.Domain;
using Kantonal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Tests.Infrastructure;

public class EfFinanceRepositoryTests
{
    private static KantonalDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<KantonalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new KantonalDbContext(options);
    }

    private static FinanceIndicators Ind(decimal? selfFinancing, decimal? netDebt) =>
        new(selfFinancing, null, null, null, null, null, netDebt, null, null);

    private static MunicipalFinanceRecord Rec(int bfs, string name, int year, decimal? selfFinancing)
        => new(BfsNumber.Create(bfs), name, year,
            new FinanceIndicators(selfFinancing, null, null, null, null, null, null, null, null));

    [Fact]
    public async Task QueryAsync_FiltersByMunicipalitySubstring_CaseInsensitive()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Aadorf", 2024, 1m), Rec(2, "Affeltrangen", 2024, 2m), Rec(3, "Bürglen", 2024, 3m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var q = new FinanceQuery("aff", null, FinanceSortField.MunicipalityName, SortDirection.Asc, 0, 50);
        var result = await repo.QueryAsync(q, CancellationToken.None);
        var count = await repo.CountAsync("aff", null, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal("Affeltrangen", Assert.Single(result).MunicipalityName);
    }

    [Fact]
    public async Task QueryAsync_FiltersByYear()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(Rec(1, "Aadorf", 2023, 1m), Rec(1, "Aadorf", 2024, 2m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var result = await repo.QueryAsync(
            new FinanceQuery(null, 2023, FinanceSortField.Year, SortDirection.Asc, 0, 50), CancellationToken.None);

        Assert.Equal(2023, Assert.Single(result).Year);
    }

    [Fact]
    public async Task QueryAsync_SortsByRatioDescending_NullsLast()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Low", 2024, 10m), Rec(2, "High", 2024, 99m), Rec(3, "Null", 2024, null));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var result = await repo.QueryAsync(
            new FinanceQuery(null, null, FinanceSortField.SelfFinancingRatio, SortDirection.Desc, 0, 50),
            CancellationToken.None);

        Assert.Equal(new[] { "High", "Low", "Null" }, result.Select(r => r.MunicipalityName).ToArray());
    }

    [Fact]
    public async Task QueryAsync_SortsByRatioAscending_NullsLast()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Low", 2024, 10m), Rec(2, "High", 2024, 99m), Rec(3, "Null", 2024, null));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var result = await repo.QueryAsync(
            new FinanceQuery(null, null, FinanceSortField.SelfFinancingRatio, SortDirection.Asc, 0, 50),
            CancellationToken.None);

        Assert.Equal(new[] { "Low", "High", "Null" }, result.Select(r => r.MunicipalityName).ToArray());
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsRecordOrNull()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.Add(Rec(4551, "Aadorf", 2024, 1m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        Assert.NotNull(await repo.GetByKeyAsync(BfsNumber.Create(4551), 2024, CancellationToken.None));
        Assert.Null(await repo.GetByKeyAsync(BfsNumber.Create(9999), 2024, CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_OrdersByNameAndPaginates()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            new MunicipalFinanceRecord(BfsNumber.Create(2), "Bravo", 2024, Ind(1m, 1m)),
            new MunicipalFinanceRecord(BfsNumber.Create(1), "Alpha", 2024, Ind(2m, 2m)),
            new MunicipalFinanceRecord(BfsNumber.Create(3), "Charlie", 2024, Ind(3m, 3m)));
        await ctx.SaveChangesAsync();

        var repo = new EfFinanceRepository(ctx);
        var page = await repo.QueryAsync(
            new FinanceQuery(null, null, FinanceSortField.MunicipalityName, SortDirection.Asc, Skip: 1, Take: 1),
            CancellationToken.None);
        var total = await repo.CountAsync(null, null, CancellationToken.None);

        Assert.Equal(3, total);
        Assert.Single(page);
        Assert.Equal("Bravo", page[0].MunicipalityName);
    }

    [Fact]
    public async Task Upsert_PersistsAllNineRatios()
    {
        var opts = new DbContextOptionsBuilder<KantonalDbContext>()
            .UseInMemoryDatabase("nine-ratios").Options;
        var indicators = new FinanceIndicators(1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m);

        await using (var ctx = new KantonalDbContext(opts))
        {
            var repo = new EfFinanceRepository(ctx);
            await repo.UpsertManyAsync(new[]
            {
                new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, indicators)
            }, CancellationToken.None);
        }

        await using (var verify = new KantonalDbContext(opts))
        {
            var loaded = await verify.FinanceRecords.SingleAsync();
            Assert.Equal(indicators, loaded.Indicators);
        }
    }

    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsDistinctMunicipalitiesSortedAndYearsDescending()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Bürglen", 2023, 1m), Rec(1, "Bürglen", 2024, 2m),
            Rec(2, "Aadorf", 2024, 3m), Rec(3, "Aadorf", 2022, 4m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var options = await repo.GetFilterOptionsAsync(CancellationToken.None);

        Assert.Equal(new[] { "Aadorf", "Bürglen" }, options.Municipalities.ToArray());
        Assert.Equal(new[] { 2024, 2023, 2022 }, options.Years.ToArray());
    }

    [Fact]
    public async Task UpsertManyAsync_InsertsNewAndUpdatesExisting()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.Add(
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, Ind(163.81m, 1415.95m)));
        await ctx.SaveChangesAsync();

        var repo = new EfFinanceRepository(ctx);
        var affected = await repo.UpsertManyAsync(new[]
        {
            // same key (4551, 2024) -> update
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, Ind(200.00m, 999.99m)),
            // new key -> insert
            new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, Ind(80.36m, -683.62m)),
        }, CancellationToken.None);

        Assert.Equal(2, affected);

        var all = await ctx.FinanceRecords.OrderBy(r => r.MunicipalityName).ToListAsync();
        Assert.Equal(2, all.Count);

        var aadorf = all.Single(r => r.BfsNumber == BfsNumber.Create(4551));
        Assert.Equal(200.00m, aadorf.SelfFinancingRatio);
        Assert.Equal(999.99m, aadorf.NetDebtPerCapitaChf);

        Assert.Contains(all, r => r.MunicipalityName == "Affeltrangen");
    }
}
