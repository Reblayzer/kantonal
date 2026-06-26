using System.Net;
using System.Text;
using Kantonal.Web.Services;

namespace Kantonal.Tests.Web;

public class FinanceApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public StubHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task GetAsync_UnwrapsEnvelopeIntoRows()
    {
        const string json = """
        {"ok":true,"data":{"items":[
          {"bfsNumber":4551,"municipalityName":"Aadorf","year":2024,
           "selfFinancingRatio":163.81,"selfFinancingShare":20.20,"interestBurdenShare":0.63,
           "capitalServiceShare":6.81,"investmentShare":14.01,"grossDebtShare":141.04,
           "netDebtPerCapitaChf":1415.95,"netDebtQuotient":105.81,"balanceSheetSurplusQuotient":128.37}
        ],"page":1,"pageSize":20,"total":3}}
        """;
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("http://api.test") };
        var client = new FinanceApiClient(http);

        var page = await client.GetAsync(1, 20, CancellationToken.None);

        Assert.Equal(3, page.Total);
        Assert.Single(page.Items);
        Assert.Equal("Aadorf", page.Items[0].MunicipalityName);
        Assert.Equal(163.81m, page.Items[0].SelfFinancingRatio);
        Assert.Equal(141.04m, page.Items[0].GrossDebtShare);
        Assert.Equal(128.37m, page.Items[0].BalanceSheetSurplusQuotient);
    }
}
