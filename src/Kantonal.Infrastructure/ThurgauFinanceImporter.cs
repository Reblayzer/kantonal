using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kantonal.Application;
using Kantonal.Domain;

namespace Kantonal.Infrastructure;

/// <summary>
/// Imports municipal finance records from the Kanton Thurgau opendata.swiss dataset
/// (Opendatasoft records API, dataset "sk-stat-4"). Expects HttpClient.BaseAddress = https://data.tg.ch/.
/// </summary>
public sealed class ThurgauFinanceImporter : IFinanceImportSource
{
    private const string RecordsPath = "api/v2/catalog/datasets/sk-stat-4/records";
    private const int MaxPageSize = 100;

    private readonly HttpClient _http;
    private readonly int _pageSize;

    public ThurgauFinanceImporter(HttpClient http, int pageSize = MaxPageSize)
    {
        _http = http;
        _pageSize = pageSize is > 0 and <= MaxPageSize ? pageSize : MaxPageSize;
    }

    public async Task<IReadOnlyList<MunicipalFinanceRecord>> FetchAllAsync(CancellationToken ct)
    {
        var all = new List<MunicipalFinanceRecord>();
        var offset = 0;

        while (true)
        {
            var url = $"{RecordsPath}?limit={_pageSize}&offset={offset}";
            var page = await _http.GetFromJsonAsync<RecordsResponse>(url, ct)
                ?? throw new InvalidOperationException("Finance import source returned an empty response.");

            foreach (var item in page.Records)
            {
                var record = Map(item.Record.Fields);
                if (record is not null) all.Add(record);
            }

            offset += _pageSize;
            if (offset >= page.TotalCount) break;
        }

        return all;
    }

    private static MunicipalFinanceRecord? Map(FinanceFields fields)
    {
        if (!int.TryParse(fields.BfsNumber, out var bfs)) return null;
        if (!int.TryParse(fields.Year, out var year)) return null;
        if (string.IsNullOrWhiteSpace(fields.MunicipalityName)) return null;

        return new MunicipalFinanceRecord(
            BfsNumber.Create(bfs),
            fields.MunicipalityName,
            year,
            ToDecimal(fields.SelfFinancingRatio),
            ToDecimal(fields.NetDebtPerCapitaChf));
    }

    private static decimal? ToDecimal(double? value) => value is null ? null : (decimal)value.Value;

    private sealed record RecordsResponse(
        [property: JsonPropertyName("total_count")] int TotalCount,
        [property: JsonPropertyName("records")] IReadOnlyList<RecordEnvelope> Records);

    private sealed record RecordEnvelope(
        [property: JsonPropertyName("record")] RecordData Record);

    private sealed record RecordData(
        [property: JsonPropertyName("fields")] FinanceFields Fields);

    private sealed record FinanceFields(
        [property: JsonPropertyName("bfs_nr_gemeinde")] string? BfsNumber,
        [property: JsonPropertyName("gemeinde_name")] string? MunicipalityName,
        [property: JsonPropertyName("jahr")] string? Year,
        [property: JsonPropertyName("selbstfinanzierungsgrad_in")] double? SelfFinancingRatio,
        [property: JsonPropertyName("nettoschuld_nettovermogen_pro_einwohner_in_chf")] double? NetDebtPerCapitaChf);
}
