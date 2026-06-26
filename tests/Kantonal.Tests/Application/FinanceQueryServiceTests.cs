using Kantonal.Application;
using Kantonal.Domain;

namespace Kantonal.Tests.Application;

public class FinanceQueryServiceTests
{
    private sealed class FakeRepo : IFinanceRepository
    {
        private readonly List<MunicipalFinanceRecord> _records;
        public FakeRepo(params MunicipalFinanceRecord[] records) => _records = records.ToList();

        public Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(
                _records.Skip(query.Skip).Take(query.Take).ToList());

        public Task<int> CountAsync(string? municipality, int? year, CancellationToken ct)
            => Task.FromResult(_records.Count);

        public Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct)
            => Task.FromResult(_records.FirstOrDefault(r => r.BfsNumber == bfsNumber && r.Year == year));

        public Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
            => Task.FromResult(records.Count);
    }

    private static FinanceIndicators Ind(decimal? selfFinancing, decimal? netDebt) =>
        new(selfFinancing, null, null, null, null, null, netDebt, null, null);

    private static MunicipalFinanceRecord Row(int bfs, string name)
        => new(BfsNumber.Create(bfs), name, 2024, Ind(100m, 50m));

    [Fact]
    public async Task GetPageAsync_ReturnsRequestedPageAndTotal()
    {
        var repo = new FakeRepo(new[] { Row(1, "A"), Row(2, "B"), Row(3, "C") });
        var service = new FinanceQueryService(repo);

        var result = await service.GetPageAsync(new FinanceListRequest(null, null, null, null, 2, 2), CancellationToken.None);

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
        var result = await service.GetPageAsync(new FinanceListRequest(null, null, null, null, page, size), CancellationToken.None);
        Assert.Equal(expectedPage, result.Page);
    }

    [Fact]
    public async Task GetPageAsync_ClampsPageSizeToMax100()
    {
        var service = new FinanceQueryService(new FakeRepo(Array.Empty<MunicipalFinanceRecord>()));
        var result = await service.GetPageAsync(new FinanceListRequest(null, null, null, null, 1, 9999), CancellationToken.None);
        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetPageAsync_RejectsUnknownSortField()
    {
        var service = new FinanceQueryService(new FakeRepo());
        var request = new FinanceListRequest(null, null, "not_a_field", null, 1, 20);

        await Assert.ThrowsAsync<Kantonal.Application.Errors.ValidationException>(
            () => service.GetPageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetPageAsync_RejectsUnknownSortDirection()
    {
        var service = new FinanceQueryService(new FakeRepo());
        var request = new FinanceListRequest(null, null, null, "sideways", 1, 20);

        await Assert.ThrowsAsync<Kantonal.Application.Errors.ValidationException>(
            () => service.GetPageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetByKeyAsync_ThrowsNotFound_WhenAbsent()
    {
        var service = new FinanceQueryService(new FakeRepo());

        await Assert.ThrowsAsync<Kantonal.Application.Errors.NotFoundException>(
            () => service.GetByKeyAsync(9999, 2024, CancellationToken.None));
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsDto_WhenPresent()
    {
        var record = new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024,
            new FinanceIndicators(163.81m, null, null, null, null, null, 1415.95m, null, null));
        var service = new FinanceQueryService(new FakeRepo(record));

        var dto = await service.GetByKeyAsync(4551, 2024, CancellationToken.None);

        Assert.Equal("Aadorf", dto.MunicipalityName);
        Assert.Equal(163.81m, dto.SelfFinancingRatio);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByKeyAsync_RejectsNonPositiveBfsNumber(int bfsNumber)
    {
        var service = new FinanceQueryService(new FakeRepo());

        await Assert.ThrowsAsync<Kantonal.Application.Errors.ValidationException>(
            () => service.GetByKeyAsync(bfsNumber, 2024, CancellationToken.None));
    }
}
