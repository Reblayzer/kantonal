namespace Kantonal.Application;

/// <summary>Imports finance records: fetch from the source, then upsert into the repository.</summary>
public sealed class FinanceImportService
{
    private readonly IFinanceImportSource _source;
    private readonly IFinanceRepository _repository;

    public FinanceImportService(IFinanceImportSource source, IFinanceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(repository);
        _source = source;
        _repository = repository;
    }

    public async Task<int> ImportAsync(CancellationToken ct)
    {
        var records = await _source.FetchAllAsync(ct);
        return await _repository.UpsertManyAsync(records, ct);
    }
}
