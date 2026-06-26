using System.Net;
using System.Net.Http.Json;
using Kantonal.Application;
using Kantonal.Domain;
using Kantonal.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kantonal.Tests.Api;

public class FinanceEndpointTests : IClassFixture<FinanceEndpointTests.TestApi>
{
    private readonly TestApi _api;
    public FinanceEndpointTests(TestApi api) => _api = api;

    private static FinanceIndicators Ind(decimal? selfFinancing, decimal? netDebt) =>
        new(selfFinancing, null, null, null, null, null, netDebt, null, null);

    [Fact]
    public async Task GetFinance_ReturnsOkEnvelopeWithItems()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Envelope>();
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.True(body.Data!.Total >= 3);
        Assert.NotEmpty(body.Data.Items);

        var aadorf = body.Data.Items.FirstOrDefault(i => i.MunicipalityName == "Aadorf");
        Assert.NotNull(aadorf);
        Assert.Equal(163.81m, aadorf!.SelfFinancingRatio);
        Assert.Equal(1415.95m, aadorf.NetDebtPerCapitaChf);
    }

    [Fact]
    public async Task PostImport_ReturnsOkEnvelopeWithImportedCount()
    {
        var client = _api.CreateClient();
        var response = await client.PostAsync("/api/import", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImportEnvelope>();
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.NotNull(body.Data);
        Assert.Equal(3, body.Data!.Imported);
    }

    public record Envelope(bool Ok, Data? Data);
    public record Data(List<Item> Items, int Page, int PageSize, int Total);
    public record Item(
        int BfsNumber,
        string MunicipalityName,
        int Year,
        decimal? SelfFinancingRatio,
        decimal? SelfFinancingShare,
        decimal? InterestBurdenShare,
        decimal? CapitalServiceShare,
        decimal? InvestmentShare,
        decimal? GrossDebtShare,
        decimal? NetDebtPerCapitaChf,
        decimal? NetDebtQuotient,
        decimal? BalanceSheetSurplusQuotient);
    public record ImportEnvelope(bool Ok, ImportData? Data);
    public record ImportData(int Imported);

    public class TestApi : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<KantonalDbContext>))
                    .ToList();
                foreach (var descriptor in descriptors)
                    services.Remove(descriptor);
                services.AddDbContext<KantonalDbContext>(o => o.UseInMemoryDatabase("api-tests"));

                // Swap the live importer for a deterministic, offline fake.
                var sourceDescriptors = services
                    .Where(d => d.ServiceType == typeof(IFinanceImportSource))
                    .ToList();
                foreach (var descriptor in sourceDescriptors)
                    services.Remove(descriptor);
                services.AddSingleton<IFinanceImportSource, FakeFinanceImportSource>();
            });
        }
    }

    private sealed class FakeFinanceImportSource : IFinanceImportSource
    {
        public Task<IReadOnlyList<MunicipalFinanceRecord>> FetchAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(new[]
            {
                new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, Ind(163.81m, 1415.95m)),
                new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, Ind(80.36m, -683.62m)),
                new MunicipalFinanceRecord(BfsNumber.Create(4486), "Amlikon-Bissegg", 2024, Ind(95.10m, 210.40m)),
            });
    }
}
