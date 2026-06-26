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

    [Fact]
    public async Task GetAsync_OrdersByNameAndPaginates()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            new MunicipalFinanceRecord(BfsNumber.Create(2), "Bravo", 2024, 1m, 1m),
            new MunicipalFinanceRecord(BfsNumber.Create(1), "Alpha", 2024, 2m, 2m),
            new MunicipalFinanceRecord(BfsNumber.Create(3), "Charlie", 2024, 3m, 3m));
        await ctx.SaveChangesAsync();

        var repo = new EfFinanceRepository(ctx);
        var page = await repo.GetAsync(skip: 1, take: 1, CancellationToken.None);
        var total = await repo.CountAsync(CancellationToken.None);

        Assert.Equal(3, total);
        Assert.Single(page);
        Assert.Equal("Bravo", page[0].MunicipalityName);
    }
}
