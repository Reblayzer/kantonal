using System.Net.Http.Json;
using System.Text.Json;
using Kantonal.Web.Models;

namespace Kantonal.Web.Services;

public class FinanceApiClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    public FinanceApiClient(HttpClient http) => _http = http;

    private record PageEnvelope(bool Ok, FinancePage? Data);
    private record OptionsEnvelope(bool Ok, FilterOptions? Data);

    public async Task<FinancePage> GetAsync(FinanceQuery query, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<PageEnvelope>(BuildListUrl(query), JsonOptions, ct);

        if (envelope is not { Ok: true, Data: not null })
            throw new InvalidOperationException("Finance API returned an unsuccessful response.");

        return envelope.Data;
    }

    public async Task<FilterOptions> GetOptionsAsync(CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<OptionsEnvelope>(
            "/api/finance/options", JsonOptions, ct);

        if (envelope is not { Ok: true, Data: not null })
            throw new InvalidOperationException("Finance options API returned an unsuccessful response.");

        return envelope.Data;
    }

    private static string BuildListUrl(FinanceQuery q)
    {
        var parts = new List<string> { $"page={q.Page}", $"pageSize={q.PageSize}" };
        if (!string.IsNullOrWhiteSpace(q.Municipality))
            parts.Add($"municipality={Uri.EscapeDataString(q.Municipality)}");
        if (q.Year is int year)
            parts.Add($"year={year}");
        if (!string.IsNullOrWhiteSpace(q.SortBy))
            parts.Add($"sortBy={Uri.EscapeDataString(q.SortBy)}");
        if (!string.IsNullOrWhiteSpace(q.SortDir))
            parts.Add($"sortDir={Uri.EscapeDataString(q.SortDir)}");
        return "/api/finance?" + string.Join("&", parts);
    }
}
