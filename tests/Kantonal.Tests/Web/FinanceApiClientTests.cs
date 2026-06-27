using System.Net;
using System.Text;
using Kantonal.Web.Models;
using Kantonal.Web.Services;

namespace Kantonal.Tests.Web;

public class FinanceApiClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _json;
        public Uri? LastUri { get; private set; }
        public CapturingHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    private const string PageJson = """
    {"ok":true,"data":{"items":[
      {"bfsNumber":4551,"municipalityName":"Aadorf","year":2024,
       "selfFinancingRatio":163.81,"selfFinancingShare":20.20,"interestBurdenShare":0.63,
       "capitalServiceShare":6.81,"investmentShare":14.01,"grossDebtShare":141.04,
       "netDebtPerCapitaChf":1415.95,"netDebtQuotient":105.81,"balanceSheetSurplusQuotient":128.37}
    ],"page":1,"pageSize":20,"total":3}}
    """;

    [Fact]
    public async Task GetAsync_UnwrapsEnvelopeIntoRows()
    {
        var http = new HttpClient(new CapturingHandler(PageJson)) { BaseAddress = new Uri("http://api.test") };
        var client = new FinanceApiClient(http);

        var page = await client.GetAsync(new FinanceQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.Equal(3, page.Total);
        Assert.Equal("Aadorf", Assert.Single(page.Items).MunicipalityName);
        Assert.Equal(163.81m, page.Items[0].SelfFinancingRatio);
    }

    [Fact]
    public async Task GetAsync_BuildsQueryStringFromFiltersAndSort()
    {
        var handler = new CapturingHandler(PageJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
        var client = new FinanceApiClient(http);

        await client.GetAsync(
            new FinanceQuery("Bürglen", 2024, "SelfFinancingRatio", "Desc", 2, 15),
            CancellationToken.None);

        var query = handler.LastUri!.PathAndQuery;
        Assert.StartsWith("/api/finance?", query);
        Assert.Contains("page=2", query);
        Assert.Contains("pageSize=15", query);
        Assert.Contains("municipality=B%C3%BCrglen", query);
        Assert.Contains("year=2024", query);
        Assert.Contains("sortBy=SelfFinancingRatio", query);
        Assert.Contains("sortDir=Desc", query);
    }

    [Fact]
    public async Task GetAsync_OmitsNullFilters()
    {
        var handler = new CapturingHandler(PageJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
        var client = new FinanceApiClient(http);

        await client.GetAsync(new FinanceQuery(Page: 1, PageSize: 20), CancellationToken.None);

        var query = handler.LastUri!.PathAndQuery;
        Assert.DoesNotContain("municipality=", query);
        Assert.DoesNotContain("year=", query);
        Assert.DoesNotContain("sortBy=", query);
    }

    [Fact]
    public async Task GetOptionsAsync_ParsesMunicipalitiesAndYears()
    {
        const string json = """
        {"ok":true,"data":{"municipalities":["Aadorf","Bürglen"],"years":[2024,2023]}}
        """;
        var http = new HttpClient(new CapturingHandler(json)) { BaseAddress = new Uri("http://api.test") };
        var client = new FinanceApiClient(http);

        var options = await client.GetOptionsAsync(CancellationToken.None);

        Assert.Equal(new[] { "Aadorf", "Bürglen" }, options.Municipalities.ToArray());
        Assert.Equal(new[] { 2024, 2023 }, options.Years.ToArray());
    }
}
