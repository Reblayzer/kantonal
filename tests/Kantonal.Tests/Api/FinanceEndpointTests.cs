using System.Net;
using System.Net.Http.Json;
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
    }

    public record Envelope(bool Ok, Data? Data);
    public record Data(List<Item> Items, int Page, int PageSize, int Total);
    public record Item(int BfsNumber, string MunicipalityName, int Year);

    public class TestApi : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<KantonalDbContext>));
                services.Remove(descriptor);
                services.AddDbContext<KantonalDbContext>(o => o.UseInMemoryDatabase("api-tests"));
            });
        }
    }
}
