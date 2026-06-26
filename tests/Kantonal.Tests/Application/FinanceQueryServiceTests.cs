using Kantonal.Application;
using Kantonal.Domain;

namespace Kantonal.Tests.Application;

public class FinanceQueryServiceTests
{
    private sealed class FakeRepo : IFinanceRepository
    {
        private readonly List<MunicipalFinanceRecord> _all;
        public FakeRepo(IEnumerable<MunicipalFinanceRecord> all) => _all = all.ToList();
        public Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(_all.Skip(skip).Take(take).ToList());
        public Task<int> CountAsync(CancellationToken ct) => Task.FromResult(_all.Count);
    }

    private static MunicipalFinanceRecord Row(int bfs, string name)
        => new(BfsNumber.Create(bfs), name, 2024, 100m, 50m);

    [Fact]
    public async Task GetPageAsync_ReturnsRequestedPageAndTotal()
    {
        var repo = new FakeRepo(new[] { Row(1, "A"), Row(2, "B"), Row(3, "C") });
        var service = new FinanceQueryService(repo);

        var result = await service.GetPageAsync(page: 2, pageSize: 2, CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Single(result.Items);
        Assert.Equal("C", result.Items[0].MunicipalityName);
    }

    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(-3, 10, 1)]
    public async Task GetPageAsync_ClampsPageToAtLeastOne(int page, int size, int expectedPage)
    {
        var service = new FinanceQueryService(new FakeRepo(Array.Empty<MunicipalFinanceRecord>()));
        var result = await service.GetPageAsync(page, size, CancellationToken.None);
        Assert.Equal(expectedPage, result.Page);
    }

    [Fact]
    public async Task GetPageAsync_ClampsPageSizeToMax100()
    {
        var service = new FinanceQueryService(new FakeRepo(Array.Empty<MunicipalFinanceRecord>()));
        var result = await service.GetPageAsync(1, 9999, CancellationToken.None);
        Assert.Equal(100, result.PageSize);
    }
}
