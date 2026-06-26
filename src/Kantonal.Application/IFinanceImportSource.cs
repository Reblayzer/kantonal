using Kantonal.Domain;

namespace Kantonal.Application;

/// <summary>Fetches municipal finance records from an external source (e.g. opendata.swiss).</summary>
public interface IFinanceImportSource
{
    Task<IReadOnlyList<MunicipalFinanceRecord>> FetchAllAsync(CancellationToken ct);
}
