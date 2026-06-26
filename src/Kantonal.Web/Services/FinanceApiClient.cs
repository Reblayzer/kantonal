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

    private record Envelope(bool Ok, FinancePage? Data);

    public async Task<FinancePage> GetAsync(int page, int pageSize, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<Envelope>(
            $"/api/finance?page={page}&pageSize={pageSize}", JsonOptions, ct);

        if (envelope is not { Ok: true, Data: not null })
            throw new InvalidOperationException("Finance API returned an unsuccessful response.");

        return envelope.Data;
    }
}
