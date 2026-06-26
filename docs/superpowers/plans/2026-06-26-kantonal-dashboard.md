# Dashboard Controls + Chart Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Blazor `/finance` dashboard interactive — filter (municipality, year) and sort (clickable headers) controls, formatted ratio cells, and one bar chart of the top-15 municipalities by a selected ratio.

**Architecture:** Add a filter-options read path through all backend layers (repo → service → `GET /api/finance/options`) so the dropdowns can be populated from real data. Wire the Web `FinanceApiClient` to the full query surface the API already supports. Build the UI as thin `.razor` components over pure, unit-tested helpers (formatting, ratio catalog, sort state, chart geometry). The chart reuses the existing list endpoint (`sortBy=<ratio>&sortDir=Desc&pageSize=15`).

**Tech Stack:** .NET 8, ASP.NET Core Minimal API, EF Core 8 (Npgsql + InMemory for tests), Blazor Server (InteractiveServer), xUnit, `Microsoft.AspNetCore.Mvc.Testing`.

## Global Constraints

- Target framework `.NET 8`. **No new NuGet packages** (dependency-light; inline SVG, no bUnit).
- API envelope is always `{ ok: true, data }` or `{ ok: false, error: { code, message } }` (via `ApiEnvelope.Success`).
- Repository pattern: application code goes through `IFinanceRepository`, never builds queries directly.
- Formatting: the 8 ratio/share/quotient fields are **already percentages** (e.g. `163.81`) → render `"163.8 %"` (1 decimal). `NetDebtPerCapitaChf` is **CHF** → render `"1'234 CHF"` (Swiss `'` group separator). `null` → `"—"`.
- Chart: **top 15** municipalities by the selected ratio, descending; chart defaults to **latest year + Self-financing ratio**.
- Sort field names sent to the API must match the `FinanceSortField` enum names exactly: `MunicipalityName`, `Year`, `SelfFinancingRatio`, `SelfFinancingShare`, `InterestBurdenShare`, `CapitalServiceShare`, `InvestmentShare`, `GrossDebtShare`, `NetDebtPerCapitaChf`, `NetDebtQuotient`, `BalanceSheetSurplusQuotient`.
- TDD for all C# (helpers, client, repo); commit per task. `dotnet format` clean, 0 warnings.
- Component `.razor` files have no automated tests (no bUnit) — kept thin over tested helpers; verified by build + a final manual run.

## File Structure

**Backend (new/modified):**
- Modify `src/Kantonal.Application/IFinanceRepository.cs` — add `GetFilterOptionsAsync`.
- Create `src/Kantonal.Application/FinanceFilterOptions.cs` — the options contract (primitives; reused as the service return type, no separate DTO — DRY).
- Modify `src/Kantonal.Application/FinanceQueryService.cs` — add delegating `GetFilterOptionsAsync`.
- Modify `src/Kantonal.Infrastructure/EfFinanceRepository.cs` — implement `GetFilterOptionsAsync`.
- Modify `src/Kantonal.Api/Program.cs` — add `GET /api/finance/options`.

**Web (new/modified):**
- Modify `src/Kantonal.Web/Models/FinanceModels.cs` — add `FinanceQuery`, `FilterOptions`.
- Modify `src/Kantonal.Web/Services/FinanceApiClient.cs` — `GetAsync(FinanceQuery)`, `GetOptionsAsync`.
- Create `src/Kantonal.Web/Formatting/RatioFormat.cs` — `Percent`/`Chf`/null formatting.
- Create `src/Kantonal.Web/Models/RatioCatalog.cs` — `RatioUnit`, `RatioInfo`, `RatioCatalog.Ratios`.
- Create `src/Kantonal.Web/Models/SortState.cs` — sort field + direction + `Toggle`.
- Create `src/Kantonal.Web/Charting/BarChartGeometry.cs` — `Bar`, `BarChartLayout`, `Compute`.
- Create `src/Kantonal.Web/Components/Finance/FinanceTable.razor` — sortable table.
- Create `src/Kantonal.Web/Components/Finance/RatioBarChart.razor` — inline SVG chart.
- Modify `src/Kantonal.Web/Components/Pages/Finance.razor` — orchestration.
- Modify `src/Kantonal.Web/wwwroot/app.css` — minimal layout styles.

**Tests (new/modified):**
- Modify `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`
- Modify `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs` (fake repo) + `FinanceImportServiceTests.cs` (fake repo) — add the new interface method to compile.
- Modify `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`
- Modify `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs`
- Create `tests/Kantonal.Tests/Web/RatioFormatTests.cs`
- Create `tests/Kantonal.Tests/Web/RatioCatalogTests.cs`
- Create `tests/Kantonal.Tests/Web/SortStateTests.cs`
- Create `tests/Kantonal.Tests/Web/BarChartGeometryTests.cs`

---

### Task 1: Filter-options repository layer

**Files:**
- Create: `src/Kantonal.Application/FinanceFilterOptions.cs`
- Modify: `src/Kantonal.Application/IFinanceRepository.cs`
- Modify: `src/Kantonal.Infrastructure/EfFinanceRepository.cs`
- Modify: `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs` (FakeRepo), `tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs` (RecordingRepository) — implement the new method so the solution compiles.
- Test: `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`

**Interfaces:**
- Produces: `FinanceFilterOptions(IReadOnlyList<string> Municipalities, IReadOnlyList<int> Years)`; `IFinanceRepository.GetFilterOptionsAsync(CancellationToken) → Task<FinanceFilterOptions>`.

- [ ] **Step 1: Add the contract record**

Create `src/Kantonal.Application/FinanceFilterOptions.cs`:

```csharp
namespace Kantonal.Application;

public sealed record FinanceFilterOptions(
    IReadOnlyList<string> Municipalities,
    IReadOnlyList<int> Years);
```

- [ ] **Step 2: Add the interface method**

In `src/Kantonal.Application/IFinanceRepository.cs`, add to the interface body:

```csharp
    Task<FinanceFilterOptions> GetFilterOptionsAsync(CancellationToken ct);
```

- [ ] **Step 3: Write the failing repo test**

In `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`, add:

```csharp
    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsDistinctMunicipalitiesSortedAndYearsDescending()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Bürglen", 2023, 1m), Rec(1, "Bürglen", 2024, 2m),
            Rec(2, "Aadorf", 2024, 3m), Rec(3, "Aadorf", 2022, 4m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var options = await repo.GetFilterOptionsAsync(CancellationToken.None);

        Assert.Equal(new[] { "Aadorf", "Bürglen" }, options.Municipalities.ToArray());
        Assert.Equal(new[] { 2024, 2023, 2022 }, options.Years.ToArray());
    }
```

- [ ] **Step 4: Run the test to verify it fails to compile**

Run: `dotnet build`
Expected: FAIL — `EfFinanceRepository` does not implement `GetFilterOptionsAsync` (and the two test fakes don't either).

- [ ] **Step 5: Implement in EF repository**

In `src/Kantonal.Infrastructure/EfFinanceRepository.cs`, add this method to the class:

```csharp
    public async Task<FinanceFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
    {
        var municipalities = await _db.FinanceRecords
            .Select(r => r.MunicipalityName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(ct);

        var years = await _db.FinanceRecords
            .Select(r => r.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync(ct);

        return new FinanceFilterOptions(municipalities, years);
    }
```

- [ ] **Step 6: Make the two test fakes compile**

In `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs`, add to the `FakeRepo` class:

```csharp
        public Task<FinanceFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
            => Task.FromResult(new FinanceFilterOptions(
                _records.Select(r => r.MunicipalityName).Distinct().OrderBy(n => n).ToList(),
                _records.Select(r => r.Year).Distinct().OrderByDescending(y => y).ToList()));
```

In `tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs`, add to the `RecordingRepository` class:

```csharp
        public Task<FinanceFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
            => Task.FromResult(new FinanceFilterOptions(Array.Empty<string>(), Array.Empty<int>()));
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EfFinanceRepositoryTests"`
Expected: PASS (all repo tests green).

- [ ] **Step 8: Commit**

```bash
git add src/Kantonal.Application/FinanceFilterOptions.cs src/Kantonal.Application/IFinanceRepository.cs src/Kantonal.Infrastructure/EfFinanceRepository.cs tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs
git commit -m "feat(infra): repository method for distinct filter options"
```

---

### Task 2: Filter-options service method + API endpoint

**Files:**
- Modify: `src/Kantonal.Application/FinanceQueryService.cs`
- Modify: `src/Kantonal.Api/Program.cs:44` (after the by-key endpoint)
- Test: `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`

**Interfaces:**
- Consumes: `IFinanceRepository.GetFilterOptionsAsync` (Task 1), `ApiEnvelope.Success`.
- Produces: `FinanceQueryService.GetFilterOptionsAsync(CancellationToken) → Task<FinanceFilterOptions>`; route `GET /api/finance/options` → `{ ok, data: { municipalities, years } }`.

- [ ] **Step 1: Add the service method**

In `src/Kantonal.Application/FinanceQueryService.cs`, add to the class (it is a thin delegate; coverage comes from the endpoint integration test below):

```csharp
    public Task<FinanceFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
        => _repo.GetFilterOptionsAsync(ct);
```

- [ ] **Step 2: Write the failing endpoint test**

In `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`, add the test and its response shapes:

```csharp
    [Fact]
    public async Task GetFinanceOptions_ReturnsDistinctMunicipalitiesAndYears()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance/options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OptionsEnvelope>();
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { "Aadorf", "Affeltrangen", "Amlikon-Bissegg" }, body.Data!.Municipalities.ToArray());
        Assert.Equal(new[] { 2024 }, body.Data.Years.ToArray());
    }

    public record OptionsEnvelope(bool Ok, OptionsData? Data);
    public record OptionsData(List<string> Municipalities, List<int> Years);
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~GetFinanceOptions_ReturnsDistinctMunicipalitiesAndYears"`
Expected: FAIL — 404 Not Found (route not mapped yet).

- [ ] **Step 4: Map the endpoint**

In `src/Kantonal.Api/Program.cs`, immediately after the `app.MapGet("/api/finance/{bfs:int}/{year:int}", ...)` block, add:

```csharp
app.MapGet("/api/finance/options", async (FinanceQueryService service, CancellationToken ct) =>
{
    var options = await service.GetFilterOptionsAsync(ct);
    return Results.Ok(ApiEnvelope.Success(options));
});
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~GetFinanceOptions_ReturnsDistinctMunicipalitiesAndYears"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Kantonal.Application/FinanceQueryService.cs src/Kantonal.Api/Program.cs tests/Kantonal.Tests/Api/FinanceEndpointTests.cs
git commit -m "feat(api): GET /api/finance/options for dropdown values"
```

---

### Task 3: Web client — full query surface + options

**Files:**
- Modify: `src/Kantonal.Web/Models/FinanceModels.cs`
- Modify: `src/Kantonal.Web/Services/FinanceApiClient.cs`
- Modify: `src/Kantonal.Web/Components/Pages/Finance.razor:53` (update the one call site to the new signature, minimally)
- Test: `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs`

**Interfaces:**
- Produces: `FinanceQuery(string? Municipality, int? Year, string? SortBy, string? SortDir, int Page, int PageSize)`; `FilterOptions(IReadOnlyList<string> Municipalities, IReadOnlyList<int> Years)`; `FinanceApiClient.GetAsync(FinanceQuery, CancellationToken) → Task<FinancePage>`; `FinanceApiClient.GetOptionsAsync(CancellationToken) → Task<FilterOptions>`.

- [ ] **Step 1: Add the Web models**

In `src/Kantonal.Web/Models/FinanceModels.cs`, append:

```csharp
public sealed record FinanceQuery(
    string? Municipality = null,
    int? Year = null,
    string? SortBy = null,
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20);

public sealed record FilterOptions(
    IReadOnlyList<string> Municipalities,
    IReadOnlyList<int> Years);
```

- [ ] **Step 2: Write the failing client tests**

Replace the body of `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs` with (keeps the existing parse assertion, switches it to the new signature, and adds URL-building + options tests):

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build`
Expected: FAIL — `GetAsync(FinanceQuery, ...)` and `GetOptionsAsync` do not exist.

- [ ] **Step 4: Rewrite the client**

Replace the body of `src/Kantonal.Web/Services/FinanceApiClient.cs` with:

```csharp
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
```

- [ ] **Step 5: Update the one existing call site (keep build green)**

In `src/Kantonal.Web/Components/Pages/Finance.razor`, change the `OnInitializedAsync` line:

```csharp
    protected override async Task OnInitializedAsync()
        => _page = await Api.GetAsync(new FinanceQuery(Page: 1, PageSize: 20), CancellationToken.None);
```

(The page is fully rewritten in Task 10; this minimal change just keeps the build green now.)

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FinanceApiClientTests"`
Expected: PASS (4 tests green).

- [ ] **Step 7: Commit**

```bash
git add src/Kantonal.Web/Models/FinanceModels.cs src/Kantonal.Web/Services/FinanceApiClient.cs src/Kantonal.Web/Components/Pages/Finance.razor tests/Kantonal.Tests/Web/FinanceApiClientTests.cs
git commit -m "feat(web): client supports filters, sort, and options endpoint"
```

---

### Task 4: RatioFormat helper

**Files:**
- Create: `src/Kantonal.Web/Formatting/RatioFormat.cs`
- Test: `tests/Kantonal.Tests/Web/RatioFormatTests.cs`

**Interfaces:**
- Produces: `RatioFormat.Percent(decimal?) → string`, `RatioFormat.Chf(decimal?) → string`, `RatioFormat.Empty = "—"`.

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Web/RatioFormatTests.cs`:

```csharp
using Kantonal.Web.Formatting;

namespace Kantonal.Tests.Web;

public class RatioFormatTests
{
    [Theory]
    [InlineData(85.42, "85.4 %")]
    [InlineData(163.81, "163.8 %")]
    [InlineData(0, "0.0 %")]
    [InlineData(1234.5, "1'234.5 %")]
    public void Percent_FormatsToOneDecimalWithSwissGrouping(double value, string expected)
        => Assert.Equal(expected, RatioFormat.Percent((decimal)value));

    [Theory]
    [InlineData(1234, "1'234 CHF")]
    [InlineData(1234567, "1'234'567 CHF")]
    [InlineData(-684, "-684 CHF")]
    public void Chf_FormatsAsWholeChfWithSwissGrouping(double value, string expected)
        => Assert.Equal(expected, RatioFormat.Chf((decimal)value));

    [Fact]
    public void NullValues_RenderAsEmDash()
    {
        Assert.Equal("—", RatioFormat.Percent(null));
        Assert.Equal("—", RatioFormat.Chf(null));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet build`
Expected: FAIL — `RatioFormat` does not exist.

- [ ] **Step 3: Implement the helper**

Create `src/Kantonal.Web/Formatting/RatioFormat.cs`:

```csharp
using System.Globalization;

namespace Kantonal.Web.Formatting;

/// <summary>
/// Deterministic, culture-independent formatting for finance figures.
/// Percentages render with one decimal; CHF as whole francs; both use the
/// Swiss apostrophe group separator. Null renders as an em dash.
/// </summary>
public static class RatioFormat
{
    public const string Empty = "—";

    private static readonly NumberFormatInfo Swiss = new()
    {
        NumberGroupSeparator = "'",
        NumberDecimalSeparator = ".",
        NumberGroupSizes = new[] { 3 },
    };

    public static string Percent(decimal? value)
        => value is null ? Empty : value.Value.ToString("N1", Swiss) + " %";

    public static string Chf(decimal? value)
        => value is null ? Empty : value.Value.ToString("N0", Swiss) + " CHF";
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~RatioFormatTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Web/Formatting/RatioFormat.cs tests/Kantonal.Tests/Web/RatioFormatTests.cs
git commit -m "feat(web): RatioFormat helper for percent/CHF/null cells"
```

---

### Task 5: RatioCatalog helper

**Files:**
- Create: `src/Kantonal.Web/Models/RatioCatalog.cs`
- Test: `tests/Kantonal.Tests/Web/RatioCatalogTests.cs`

**Interfaces:**
- Consumes: `RatioFormat` (Task 4), `FinanceRow` (existing).
- Produces: `enum RatioUnit { Percent, Chf }`; `RatioInfo(string Key, string Label, RatioUnit Unit, Func<FinanceRow, decimal?> Selector)` with `string Format(FinanceRow row)`; `RatioCatalog.Ratios : IReadOnlyList<RatioInfo>` (9 entries, `Key` = `FinanceSortField` name).

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Web/RatioCatalogTests.cs`:

```csharp
using Kantonal.Web.Models;

namespace Kantonal.Tests.Web;

public class RatioCatalogTests
{
    private static FinanceRow FullRow() => new(
        BfsNumber: 1, MunicipalityName: "X", Year: 2024,
        SelfFinancingRatio: 1m, SelfFinancingShare: 2m, InterestBurdenShare: 3m,
        CapitalServiceShare: 4m, InvestmentShare: 5m, GrossDebtShare: 6m,
        NetDebtPerCapitaChf: 7m, NetDebtQuotient: 8m, BalanceSheetSurplusQuotient: 9m);

    [Fact]
    public void Ratios_ExposesAllNineFieldsWithExpectedKeys()
    {
        var keys = RatioCatalog.Ratios.Select(r => r.Key).ToArray();
        Assert.Equal(new[]
        {
            "SelfFinancingRatio", "SelfFinancingShare", "InterestBurdenShare",
            "CapitalServiceShare", "InvestmentShare", "GrossDebtShare",
            "NetDebtPerCapitaChf", "NetDebtQuotient", "BalanceSheetSurplusQuotient",
        }, keys);
    }

    [Fact]
    public void Selectors_ReturnTheMatchingProperty()
    {
        var row = FullRow();
        Assert.Equal(1m, RatioCatalog.Ratios.Single(r => r.Key == "SelfFinancingRatio").Selector(row));
        Assert.Equal(7m, RatioCatalog.Ratios.Single(r => r.Key == "NetDebtPerCapitaChf").Selector(row));
        Assert.Equal(9m, RatioCatalog.Ratios.Single(r => r.Key == "BalanceSheetSurplusQuotient").Selector(row));
    }

    [Fact]
    public void Format_UsesPercentForRatiosAndChfForPerCapita()
    {
        var row = FullRow();
        Assert.Equal("1.0 %", RatioCatalog.Ratios.Single(r => r.Key == "SelfFinancingRatio").Format(row));
        Assert.Equal("7 CHF", RatioCatalog.Ratios.Single(r => r.Key == "NetDebtPerCapitaChf").Format(row));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet build`
Expected: FAIL — `RatioCatalog`/`RatioInfo`/`RatioUnit` do not exist.

- [ ] **Step 3: Implement the catalog**

Create `src/Kantonal.Web/Models/RatioCatalog.cs`:

```csharp
using Kantonal.Web.Formatting;

namespace Kantonal.Web.Models;

public enum RatioUnit { Percent, Chf }

/// <summary>One HRM2 ratio: its API sort key, display label, unit, and accessor.</summary>
public sealed record RatioInfo(string Key, string Label, RatioUnit Unit, Func<FinanceRow, decimal?> Selector)
{
    public string Format(FinanceRow row) => Unit == RatioUnit.Percent
        ? RatioFormat.Percent(Selector(row))
        : RatioFormat.Chf(Selector(row));
}

/// <summary>Single source of truth for the nine ratios. Keys match FinanceSortField names.</summary>
public static class RatioCatalog
{
    public static readonly IReadOnlyList<RatioInfo> Ratios = new[]
    {
        new RatioInfo("SelfFinancingRatio", "Self-financing ratio", RatioUnit.Percent, r => r.SelfFinancingRatio),
        new RatioInfo("SelfFinancingShare", "Self-financing share", RatioUnit.Percent, r => r.SelfFinancingShare),
        new RatioInfo("InterestBurdenShare", "Interest burden share", RatioUnit.Percent, r => r.InterestBurdenShare),
        new RatioInfo("CapitalServiceShare", "Capital service share", RatioUnit.Percent, r => r.CapitalServiceShare),
        new RatioInfo("InvestmentShare", "Investment share", RatioUnit.Percent, r => r.InvestmentShare),
        new RatioInfo("GrossDebtShare", "Gross debt share", RatioUnit.Percent, r => r.GrossDebtShare),
        new RatioInfo("NetDebtPerCapitaChf", "Net debt/capita", RatioUnit.Chf, r => r.NetDebtPerCapitaChf),
        new RatioInfo("NetDebtQuotient", "Net debt quotient", RatioUnit.Percent, r => r.NetDebtQuotient),
        new RatioInfo("BalanceSheetSurplusQuotient", "Balance-sheet surplus quotient", RatioUnit.Percent, r => r.BalanceSheetSurplusQuotient),
    };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~RatioCatalogTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Web/Models/RatioCatalog.cs tests/Kantonal.Tests/Web/RatioCatalogTests.cs
git commit -m "feat(web): RatioCatalog as single source of truth for the 9 ratios"
```

---

### Task 6: SortState helper

**Files:**
- Create: `src/Kantonal.Web/Models/SortState.cs`
- Test: `tests/Kantonal.Tests/Web/SortStateTests.cs`

**Interfaces:**
- Produces: `SortState(string Field, string Direction)` with `Asc`/`Desc` constants, `SortState.Default`, and `SortState Toggle(string clickedField)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Web/SortStateTests.cs`:

```csharp
using Kantonal.Web.Models;

namespace Kantonal.Tests.Web;

public class SortStateTests
{
    [Fact]
    public void Default_IsMunicipalityNameAscending()
    {
        Assert.Equal("MunicipalityName", SortState.Default.Field);
        Assert.Equal(SortState.Asc, SortState.Default.Direction);
    }

    [Fact]
    public void Toggle_SameField_FlipsDirection()
    {
        var asc = new SortState("Year", SortState.Asc);
        var flipped = asc.Toggle("Year");
        Assert.Equal("Year", flipped.Field);
        Assert.Equal(SortState.Desc, flipped.Direction);
        Assert.Equal(SortState.Asc, flipped.Toggle("Year").Direction);
    }

    [Fact]
    public void Toggle_NewField_SelectsItAscending()
    {
        var state = new SortState("Year", SortState.Desc);
        var next = state.Toggle("SelfFinancingRatio");
        Assert.Equal("SelfFinancingRatio", next.Field);
        Assert.Equal(SortState.Asc, next.Direction);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet build`
Expected: FAIL — `SortState` does not exist.

- [ ] **Step 3: Implement the helper**

Create `src/Kantonal.Web/Models/SortState.cs`:

```csharp
namespace Kantonal.Web.Models;

/// <summary>Current table sort. Direction strings match the API's SortDirection enum.</summary>
public sealed record SortState(string Field, string Direction)
{
    public const string Asc = "Asc";
    public const string Desc = "Desc";

    public static readonly SortState Default = new("MunicipalityName", Asc);

    /// <summary>Clicking the active column flips direction; clicking a new column sorts it ascending.</summary>
    public SortState Toggle(string clickedField)
        => clickedField == Field
            ? this with { Direction = Direction == Asc ? Desc : Asc }
            : new SortState(clickedField, Asc);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SortStateTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Web/Models/SortState.cs tests/Kantonal.Tests/Web/SortStateTests.cs
git commit -m "feat(web): SortState toggle logic for sortable headers"
```

---

### Task 7: BarChartGeometry helper

**Files:**
- Create: `src/Kantonal.Web/Charting/BarChartGeometry.cs`
- Test: `tests/Kantonal.Tests/Web/BarChartGeometryTests.cs`

**Interfaces:**
- Produces: `readonly record struct Bar(double X, double Y, double Width, double Height)`; `BarChartLayout(IReadOnlyList<Bar> Bars, decimal MaxValue)`; `BarChartGeometry.Compute(IReadOnlyList<decimal> values, double width, double height, double gap = 4) → BarChartLayout`.

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Web/BarChartGeometryTests.cs`:

```csharp
using Kantonal.Web.Charting;

namespace Kantonal.Tests.Web;

public class BarChartGeometryTests
{
    [Fact]
    public void Compute_EmptyInput_ReturnsNoBars()
    {
        var layout = BarChartGeometry.Compute(Array.Empty<decimal>(), 100, 200);
        Assert.Empty(layout.Bars);
        Assert.Equal(0m, layout.MaxValue);
    }

    [Fact]
    public void Compute_ScalesHeightsToMaxAndLaysOutLeftToRight()
    {
        var layout = BarChartGeometry.Compute(new[] { 50m, 100m }, 100, 200, gap: 0);

        Assert.Equal(100m, layout.MaxValue);
        Assert.Equal(2, layout.Bars.Count);
        // bar width = (100 - 0) / 2 = 50
        Assert.Equal(50d, layout.Bars[0].Width, 3);
        Assert.Equal(0d, layout.Bars[0].X, 3);
        Assert.Equal(50d, layout.Bars[1].X, 3);
        // heights scale to max: 50/100*200=100, 100/100*200=200
        Assert.Equal(100d, layout.Bars[0].Height, 3);
        Assert.Equal(200d, layout.Bars[1].Height, 3);
        // y is the top of the bar (height - barHeight)
        Assert.Equal(100d, layout.Bars[0].Y, 3);
        Assert.Equal(0d, layout.Bars[1].Y, 3);
    }

    [Fact]
    public void Compute_AllZero_ProducesZeroHeightBarsWithoutDividingByZero()
    {
        var layout = BarChartGeometry.Compute(new[] { 0m, 0m }, 100, 200, gap: 0);
        Assert.Equal(0m, layout.MaxValue);
        Assert.All(layout.Bars, b => Assert.Equal(0d, b.Height, 3));
    }

    [Fact]
    public void Compute_NegativeValues_ClampToZeroHeight()
    {
        var layout = BarChartGeometry.Compute(new[] { -10m, 100m }, 100, 200, gap: 0);
        Assert.Equal(0d, layout.Bars[0].Height, 3);
        Assert.Equal(200d, layout.Bars[1].Height, 3);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet build`
Expected: FAIL — `BarChartGeometry` does not exist.

- [ ] **Step 3: Implement the helper**

Create `src/Kantonal.Web/Charting/BarChartGeometry.cs`:

```csharp
namespace Kantonal.Web.Charting;

public readonly record struct Bar(double X, double Y, double Width, double Height);

public sealed record BarChartLayout(IReadOnlyList<Bar> Bars, decimal MaxValue);

/// <summary>
/// Pure layout for a simple vertical bar chart. Heights scale from a zero
/// baseline to the maximum value; negative values clamp to zero height
/// (the chart is fed top-N-descending data, so charted values are positive).
/// </summary>
public static class BarChartGeometry
{
    public static BarChartLayout Compute(IReadOnlyList<decimal> values, double width, double height, double gap = 4)
    {
        if (values.Count == 0)
            return new BarChartLayout(Array.Empty<Bar>(), 0m);

        var max = values.Max();
        var barWidth = (width - gap * (values.Count - 1)) / values.Count;

        var bars = new List<Bar>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var barHeight = max > 0m ? Math.Max(0d, (double)(values[i] / max) * height) : 0d;
            var x = i * (barWidth + gap);
            bars.Add(new Bar(x, height - barHeight, barWidth, barHeight));
        }

        return new BarChartLayout(bars, max);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~BarChartGeometryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Web/Charting/BarChartGeometry.cs tests/Kantonal.Tests/Web/BarChartGeometryTests.cs
git commit -m "feat(web): BarChartGeometry pure layout helper"
```

---

### Task 8: FinanceTable component

**Files:**
- Create: `src/Kantonal.Web/Components/Finance/FinanceTable.razor`

**Interfaces:**
- Consumes: `FinanceRow`, `SortState`, `RatioCatalog` (Tasks 5, 6).
- Produces: `<FinanceTable Items Sort OnSort />` — `Items: IReadOnlyList<FinanceRow>`, `Sort: SortState`, `OnSort: EventCallback<string>`.

- [ ] **Step 1: Create the component**

Create `src/Kantonal.Web/Components/Finance/FinanceTable.razor`:

```razor
@using Kantonal.Web.Models

<table class="table finance-table">
    <thead>
        <tr>
            <th>BFS</th>
            <th role="button" @onclick='() => OnSort.InvokeAsync("MunicipalityName")'>Municipality @Indicator("MunicipalityName")</th>
            <th role="button" @onclick='() => OnSort.InvokeAsync("Year")'>Year @Indicator("Year")</th>
            @foreach (var ratio in RatioCatalog.Ratios)
            {
                <th role="button" @onclick="() => OnSort.InvokeAsync(ratio.Key)">@ratio.Label @Indicator(ratio.Key)</th>
            }
        </tr>
    </thead>
    <tbody>
        @foreach (var row in Items)
        {
            <tr>
                <td>@row.BfsNumber</td>
                <td>@row.MunicipalityName</td>
                <td>@row.Year</td>
                @foreach (var ratio in RatioCatalog.Ratios)
                {
                    <td class="num">@ratio.Format(row)</td>
                }
            </tr>
        }
    </tbody>
</table>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<FinanceRow> Items { get; set; } = Array.Empty<FinanceRow>();
    [Parameter, EditorRequired] public SortState Sort { get; set; } = SortState.Default;
    [Parameter] public EventCallback<string> OnSort { get; set; }

    private string Indicator(string field)
        => Sort.Field == field ? (Sort.Direction == SortState.Asc ? "▲" : "▼") : "";
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/Kantonal.Web`
Expected: PASS (0 errors).

- [ ] **Step 3: Commit**

```bash
git add src/Kantonal.Web/Components/Finance/FinanceTable.razor
git commit -m "feat(web): sortable FinanceTable component"
```

---

### Task 9: RatioBarChart component

**Files:**
- Create: `src/Kantonal.Web/Components/Finance/RatioBarChart.razor`

**Interfaces:**
- Consumes: `BarChartGeometry` (Task 7).
- Produces: `<RatioBarChart Data Title />` — nested `RatioBarChart.Datum(string Label, decimal Value)`, `Data: IReadOnlyList<Datum>`, `Title: string`.

- [ ] **Step 1: Create the component**

Create `src/Kantonal.Web/Components/Finance/RatioBarChart.razor`:

```razor
@using System.Globalization
@using Kantonal.Web.Charting

<figure class="chart">
    <figcaption>@Title</figcaption>
    @if (Data.Count == 0)
    {
        <p><em>No data to chart.</em></p>
    }
    else
    {
        var layout = BarChartGeometry.Compute(Data.Select(d => d.Value).ToList(), Width, Height);
        <svg width="@Inv(Width)" height="@Inv(Height + LabelSpace)" role="img" aria-label="@Title" class="bar-chart">
            @for (var i = 0; i < layout.Bars.Count; i++)
            {
                var bar = layout.Bars[i];
                <rect x="@Inv(bar.X)" y="@Inv(bar.Y)" width="@Inv(bar.Width)" height="@Inv(bar.Height)" fill="#3b6ea5">
                    <title>@Data[i].Label: @Data[i].Value.ToString("0.##", CultureInfo.InvariantCulture)</title>
                </rect>
                <text x="@Inv(bar.X + bar.Width / 2)" y="@Inv(Height + 10)"
                      font-size="9" text-anchor="end"
                      transform="rotate(-45 @Inv(bar.X + bar.Width / 2) @Inv(Height + 10))">@Data[i].Label</text>
            }
        </svg>
    }
</figure>

@code {
    public sealed record Datum(string Label, decimal Value);

    [Parameter, EditorRequired] public IReadOnlyList<Datum> Data { get; set; } = Array.Empty<Datum>();
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public double Width { get; set; } = 760;
    [Parameter] public double Height { get; set; } = 240;

    private const double LabelSpace = 70;

    private static string Inv(double v) => v.ToString(CultureInfo.InvariantCulture);
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/Kantonal.Web`
Expected: PASS (0 errors).

- [ ] **Step 3: Commit**

```bash
git add src/Kantonal.Web/Components/Finance/RatioBarChart.razor
git commit -m "feat(web): inline-SVG RatioBarChart component"
```

---

### Task 10: Wire up the Finance page

**Files:**
- Modify: `src/Kantonal.Web/Components/Pages/Finance.razor` (full rewrite)
- Modify: `src/Kantonal.Web/wwwroot/app.css` (append styles)

**Interfaces:**
- Consumes: `FinanceApiClient.GetAsync`/`GetOptionsAsync` (Task 3), `FinanceQuery`, `FilterOptions`, `RatioCatalog`, `SortState` (Tasks 5, 6), `FinanceTable` (Task 8), `RatioBarChart` (Task 9).

- [ ] **Step 1: Rewrite the page**

Replace the entire contents of `src/Kantonal.Web/Components/Pages/Finance.razor` with:

```razor
@page "/finance"
@using Kantonal.Web.Components.Finance
@using Kantonal.Web.Models
@using Kantonal.Web.Services
@inject FinanceApiClient Api
@rendermode InteractiveServer

<PageTitle>Kantonal — Municipal Finances</PageTitle>

<h1>Cantonal/Municipal Finances (Thurgau)</h1>

@if (_options is null)
{
    <p><em>Loading…</em></p>
}
else
{
    <section class="controls">
        <label>Municipality:
            <select @bind="_municipality" @bind:after="ApplyFilterAsync">
                <option value="">(all)</option>
                @foreach (var m in _options.Municipalities)
                {
                    <option value="@m">@m</option>
                }
            </select>
        </label>
        <label>Year:
            <select @bind="_year" @bind:after="ApplyFilterAsync">
                <option value="">(all)</option>
                @foreach (var y in _options.Years)
                {
                    <option value="@y">@y</option>
                }
            </select>
        </label>
    </section>

    <section class="controls">
        <label>Chart ratio:
            <select @bind="_chartRatioKey" @bind:after="ReloadChartAsync">
                @foreach (var r in RatioCatalog.Ratios)
                {
                    <option value="@r.Key">@r.Label</option>
                }
            </select>
        </label>
        <label>Chart year:
            <select @bind="_chartYear" @bind:after="ReloadChartAsync">
                @foreach (var y in _options.Years)
                {
                    <option value="@y">@y</option>
                }
            </select>
        </label>
    </section>

    <RatioBarChart Data="_chartData" Title="@ChartTitle" />

    @if (_page is null)
    {
        <p><em>Loading…</em></p>
    }
    else if (_page.Total == 0)
    {
        <p>No records.</p>
    }
    else
    {
        <p>@_page.Total record(s).</p>
        <FinanceTable Items="_page.Items" Sort="_sort" OnSort="OnSortAsync" />
        <div class="paging">
            <button disabled="@(_pageNumber <= 1)" @onclick="PrevPageAsync">Prev</button>
            <span>Page @_page.Page</span>
            <button disabled="@(_pageNumber * PageSize >= _page.Total)" @onclick="NextPageAsync">Next</button>
        </div>
    }
}

@code {
    private const int PageSize = 20;
    private const int ChartTopN = 15;

    private FilterOptions? _options;
    private FinancePage? _page;

    private string _municipality = "";
    private string _year = "";
    private SortState _sort = SortState.Default;
    private int _pageNumber = 1;

    private string _chartRatioKey = "SelfFinancingRatio";
    private int _chartYear;
    private IReadOnlyList<RatioBarChart.Datum> _chartData = Array.Empty<RatioBarChart.Datum>();

    private string ChartTitle =>
        $"Top {ChartTopN} — {RatioCatalog.Ratios.First(r => r.Key == _chartRatioKey).Label} ({_chartYear})";

    protected override async Task OnInitializedAsync()
    {
        _options = await Api.GetOptionsAsync(CancellationToken.None);
        _chartYear = _options.Years.FirstOrDefault();
        await ReloadTableAsync();
        await ReloadChartAsync();
    }

    private async Task ApplyFilterAsync()
    {
        _pageNumber = 1;
        await ReloadTableAsync();
    }

    private async Task ReloadTableAsync()
    {
        var query = new FinanceQuery(
            Municipality: string.IsNullOrEmpty(_municipality) ? null : _municipality,
            Year: string.IsNullOrEmpty(_year) ? null : int.Parse(_year),
            SortBy: _sort.Field,
            SortDir: _sort.Direction,
            Page: _pageNumber,
            PageSize: PageSize);
        _page = await Api.GetAsync(query, CancellationToken.None);
    }

    private async Task ReloadChartAsync()
    {
        var ratio = RatioCatalog.Ratios.First(r => r.Key == _chartRatioKey);
        var query = new FinanceQuery(
            Year: _chartYear, SortBy: _chartRatioKey, SortDir: SortState.Desc,
            Page: 1, PageSize: ChartTopN);
        var page = await Api.GetAsync(query, CancellationToken.None);

        _chartData = page.Items
            .Where(row => ratio.Selector(row) is not null)
            .Select(row => new RatioBarChart.Datum(row.MunicipalityName, ratio.Selector(row)!.Value))
            .ToList();
    }

    private async Task OnSortAsync(string field)
    {
        _sort = _sort.Toggle(field);
        _pageNumber = 1;
        await ReloadTableAsync();
    }

    private async Task PrevPageAsync()
    {
        if (_pageNumber <= 1) return;
        _pageNumber--;
        await ReloadTableAsync();
    }

    private async Task NextPageAsync()
    {
        _pageNumber++;
        await ReloadTableAsync();
    }
}
```

- [ ] **Step 2: Append minimal styles**

Append to `src/Kantonal.Web/wwwroot/app.css`:

```css
.controls { display: flex; gap: 1.5rem; margin: 0.75rem 0; flex-wrap: wrap; }
.controls label { display: flex; gap: 0.4rem; align-items: center; }
.chart { margin: 1rem 0; overflow-x: auto; }
.chart figcaption { font-weight: 600; margin-bottom: 0.5rem; }
.finance-table th[role="button"] { cursor: pointer; user-select: none; white-space: nowrap; }
.finance-table td.num { text-align: right; font-variant-numeric: tabular-nums; }
.paging { display: flex; gap: 0.75rem; align-items: center; margin-top: 0.75rem; }
```

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build && dotnet test`
Expected: PASS — build 0 warnings; all tests green (existing + the new helper/client/repo/endpoint tests).

- [ ] **Step 4: Format check**

Run: `dotnet format --verify-no-changes`
Expected: no changes needed (exit 0).

- [ ] **Step 5: Manual verification (no bUnit, so verify in the running app)**

Start the API and Web (two terminals), then open `/finance`:

```bash
# Terminal 1 — API (needs Postgres per Program.cs connection string)
dotnet run --project src/Kantonal.Api
# Terminal 2 — Web
dotnet run --project src/Kantonal.Web
```

Confirm:
- Municipality + Year dropdowns are populated; selecting one filters the table.
- Clicking a column header sorts (▲/▼ toggles on repeat clicks).
- Ratio cells show `"… %"` / `"… CHF"` / `"—"`; per-capita is the CHF column.
- The chart shows up to 15 bars, defaulting to latest year + Self-financing ratio; changing the chart pickers redraws it.
- Prev/Next paging works and disables at the ends.

(If Postgres is unavailable locally, this manual step can be deferred to review; the automated suite already covers the logic. Note the deferral in the PR.)

- [ ] **Step 6: Commit**

```bash
git add src/Kantonal.Web/Components/Pages/Finance.razor src/Kantonal.Web/wwwroot/app.css
git commit -m "feat(web): interactive finance dashboard with filters, sort, and chart"
```

---

## Self-Review

**Spec coverage:**
- Filter-options endpoint → Tasks 1–2. ✅
- Client wired to full query surface + options → Task 3. ✅
- `RatioCatalog`/`RatioFormat`/`SortToggle`(`SortState`)/`BarChartGeometry` helpers → Tasks 4–7. ✅
- `FinanceTable`/`RatioBarChart`/`Finance.razor` → Tasks 8–10. ✅
- Chart = top-15 by ratio, latest-year + Self-financing default, reuses list endpoint → Task 10 `ReloadChartAsync`. ✅
- Numeric formatting (percent 1dp, CHF `'` grouping, null `"—"`) → Task 4, applied in Task 8. ✅
- Clickable sortable headers with indicator → Tasks 6, 8. ✅
- Dropdowns from data → Tasks 1–3, 10. ✅
- Error/empty handling → client throws on non-ok (Task 3); empty result → "No records." (Task 10). ✅
- No bUnit / thin razor tradeoff → components have build-only steps + final manual run. ✅

**Deviation from spec (noted):** the service returns `FinanceFilterOptions` directly rather than a separate `FinanceFilterOptionsDto` — it is already a primitives-only contract, so a parallel DTO would be duplication (DRY). The thin service delegate is covered by the endpoint integration test rather than a near-empty unit test.

**Placeholder scan:** none — every code/test step contains complete content.

**Type consistency:** `FinanceQuery`, `FilterOptions`, `FinanceFilterOptions`, `SortState` (`Field`/`Direction`/`Asc`/`Desc`/`Toggle`/`Default`), `RatioInfo` (`Key`/`Label`/`Unit`/`Selector`/`Format`), `RatioUnit`, `Bar`/`BarChartLayout`/`Compute`, `RatioBarChart.Datum` are used consistently across producing and consuming tasks. Sort keys and the `Desc` string sent to the API match `FinanceSortField`/`SortDirection` names.
