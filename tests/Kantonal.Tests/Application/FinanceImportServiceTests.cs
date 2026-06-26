using Kantonal.Application;
using Kantonal.Domain;

namespace Kantonal.Tests.Application;

public class FinanceImportServiceTests
{
    private static FinanceIndicators Ind(decimal? selfFinancing, decimal? netDebt) =>
        new(selfFinancing, null, null, null, null, null, netDebt, null, null);

    [Fact]
    public async Task ImportAsync_UpsertsAllFetchedRecords()
    {
        var source = new StubSource(
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, Ind(163.81m, 1415.95m)),
            new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, Ind(80.36m, -683.62m)));
        var repo = new RecordingRepository();
        var service = new FinanceImportService(source, repo);

        var imported = await service.ImportAsync(CancellationToken.None);

        Assert.Equal(2, imported);
        Assert.NotNull(repo.Upserted);
        Assert.Equal(2, repo.Upserted!.Count);
        Assert.Contains(repo.Upserted, r => r.MunicipalityName == "Aadorf");
    }

    private sealed class StubSource : IFinanceImportSource
    {
        private readonly IReadOnlyList<MunicipalFinanceRecord> _records;
        public StubSource(params MunicipalFinanceRecord[] records) => _records = records;
        public Task<IReadOnlyList<MunicipalFinanceRecord>> FetchAllAsync(CancellationToken ct)
            => Task.FromResult(_records);
    }

    private sealed class RecordingRepository : IFinanceRepository
    {
        public IReadOnlyList<MunicipalFinanceRecord>? Upserted { get; private set; }

        public Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
        {
            Upserted = records;
            return Task.FromResult(records.Count);
        }

        public Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(Array.Empty<MunicipalFinanceRecord>());

        public Task<int> CountAsync(string? municipality, int? year, CancellationToken ct)
            => Task.FromResult(0);

        public Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct)
            => Task.FromResult<MunicipalFinanceRecord?>(null);
    }
}
