using System.Net;
using System.Text;
using Kantonal.Domain;
using Kantonal.Infrastructure;

namespace Kantonal.Tests.Infrastructure;

public class ThurgauFinanceImporterTests
{
    [Fact]
    public async Task FetchAllAsync_MapsFieldsFromPayload()
    {
        const string payload = """
        {
          "total_count": 1,
          "records": [
            { "record": { "fields": {
              "bfs_nr_gemeinde": "4551",
              "gemeinde_name": "Aadorf",
              "jahr": "2024",
              "selbstfinanzierungsgrad_in": 163.810552087531,
              "nettoschuld_nettovermogen_pro_einwohner_in_chf": 1415.94885488343
            }}}
          ]
        }
        """;
        var importer = ImporterReturning(_ => payload, pageSize: 100);

        var records = await importer.FetchAllAsync(CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(BfsNumber.Create(4551), record.BfsNumber);
        Assert.Equal("Aadorf", record.MunicipalityName);
        Assert.Equal(2024, record.Year);
        Assert.NotNull(record.SelfFinancingRatio);
        Assert.Equal(163.81m, Math.Round(record.SelfFinancingRatio!.Value, 2));
        Assert.NotNull(record.NetDebtPerCapitaChf);
        Assert.Equal(1415.95m, Math.Round(record.NetDebtPerCapitaChf!.Value, 2));
    }

    [Fact]
    public async Task FetchAllAsync_PagesThroughAllRecords()
    {
        // pageSize 2, total 3 -> expect two requests: offset 0 (2 rows) then offset 2 (1 row).
        var offsets = new List<string>();
        string Respond(HttpRequestMessage req)
        {
            var query = req.RequestUri!.Query;
            offsets.Add(query);
            return query.Contains("offset=0")
                ? Page(totalCount: 3, ("1", "Alpha", "2024"), ("2", "Bravo", "2024"))
                : Page(totalCount: 3, ("3", "Charlie", "2024"));
        }
        var importer = ImporterReturning(Respond, pageSize: 2);

        var records = await importer.FetchAllAsync(CancellationToken.None);

        Assert.Equal(3, records.Count);
        Assert.Equal(2, offsets.Count);
        Assert.Contains(offsets, q => q.Contains("offset=0"));
        Assert.Contains(offsets, q => q.Contains("offset=2"));
    }

    [Fact]
    public async Task FetchAllAsync_ToleratesNullRatiosAndSkipsUnparseableRows()
    {
        const string payload = """
        {
          "total_count": 3,
          "records": [
            { "record": { "fields": {
              "bfs_nr_gemeinde": "4711",
              "gemeinde_name": "Affeltrangen",
              "jahr": "2024",
              "selbstfinanzierungsgrad_in": null
            }}},
            { "record": { "fields": {
              "bfs_nr_gemeinde": "not-a-number",
              "gemeinde_name": "Broken",
              "jahr": "2024"
            }}},
            { "record": { "fields": {
              "bfs_nr_gemeinde": "4712",
              "gemeinde_name": " ",
              "jahr": "2024"
            }}}
          ]
        }
        """;
        var importer = ImporterReturning(_ => payload, pageSize: 100);

        var records = await importer.FetchAllAsync(CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal("Affeltrangen", record.MunicipalityName);
        Assert.Null(record.SelfFinancingRatio);
        Assert.Null(record.NetDebtPerCapitaChf);
    }

    [Fact]
    public async Task FetchAllAsync_ReturnsEmpty_WhenTotalCountIsZero()
    {
        var calls = 0;
        string Respond(HttpRequestMessage req)
        {
            calls++;
            return """{ "total_count": 0, "records": [] }""";
        }
        var importer = ImporterReturning(Respond, pageSize: 100);

        var records = await importer.FetchAllAsync(CancellationToken.None);

        Assert.Empty(records);
        Assert.Equal(1, calls);
    }

    private static ThurgauFinanceImporter ImporterReturning(
        Func<HttpRequestMessage, string> responder, int pageSize)
    {
        var http = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("https://data.tg.ch/"),
        };
        return new ThurgauFinanceImporter(http, pageSize);
    }

    private static string Page(int totalCount, params (string Bfs, string Name, string Year)[] rows)
    {
        var records = string.Join(",", rows.Select(r =>
            $$"""{ "record": { "fields": { "bfs_nr_gemeinde": "{{r.Bfs}}", "gemeinde_name": "{{r.Name}}", "jahr": "{{r.Year}}" } } }"""));
        return $$"""{ "total_count": {{totalCount}}, "records": [ {{records}} ] }""";
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responder;
        public StubHandler(Func<HttpRequestMessage, string> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responder(request), Encoding.UTF8, "application/json"),
            });
    }
}
