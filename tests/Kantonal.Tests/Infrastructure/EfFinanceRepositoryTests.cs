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

    [Fact]
    public async Task GetAsync_OrdersByNameAndPaginates()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            new MunicipalFinanceRecord(BfsNumber.Create(2), "Bravo", 2024, Ind(1m, 1m)),
            new MunicipalFinanceRecord(BfsNumber.Create(1), "Alpha", 2024, Ind(2m, 2m)),
            new MunicipalFinanceRecord(BfsNumber.Create(3), "Charlie", 2024, Ind(3m, 3m)));
        await ctx.SaveChangesAsync();

        var repo = new EfFinanceRepository(ctx);
        var page = await repo.GetAsync(skip: 1, take: 1, CancellationToken.None);
        var total = await repo.CountAsync(CancellationToken.None);

        Assert.Equal(3, total);
        Assert.Single(page);
        Assert.Equal("Bravo", page[0].MunicipalityName);
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
