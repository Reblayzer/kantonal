# Kantonal Import Job Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the static 3-row `DatabaseSeeder` with a real importer that pulls live Thurgau municipal-finance data from opendata.swiss and upserts it into Postgres.

**Architecture:** Add an Application port `IFinanceImportSource` (fetch domain records) and an idempotent upsert on the existing `IFinanceRepository`. A `FinanceImportService` (Application) orchestrates fetch → upsert. Infrastructure provides `ThurgauFinanceImporter` (a typed `HttpClient` paging the Opendatasoft records API and mapping JSON → domain). The Api composition root registers the typed client, runs the import at startup (failure-tolerant), and exposes a manual `POST /api/import` trigger.

**Tech Stack:** .NET 8, ASP.NET Core minimal API, EF Core 8 (Npgsql + InMemory for tests), `System.Net.Http.Json`, xUnit.

## Global Constraints

- **Clean architecture / dependency rule:** Domain ← Application ← Infrastructure/Api. The Application port `IFinanceImportSource` lives in `Kantonal.Application`; the HTTP implementation lives in `Kantonal.Infrastructure`. Application never references Infrastructure.
- **Repository pattern:** all data access stays behind `IFinanceRepository`. No queries built in Application or Api code.
- **API envelope:** responses use `ApiEnvelope.Success(...)` → `{ ok: true, data }`. Never mix shapes.
- **No network in tests:** every test stubs `HttpMessageHandler` or fakes `IFinanceImportSource`. No live HTTP call in the suite.
- **Explicit HTTP timeout:** the typed `HttpClient` sets an explicit `Timeout` (per `~/dev/CLAUDE.md` perf rules). No query inside a loop (load existing rows once for upsert).
- **TDD:** one failing test first, minimal code to green, commit per task. Stage specific files only (never `git add .`).
- **Conventional commits** with scope: `feat(infrastructure)`, `feat(application)`, `feat(api)`, `test(...)`, `docs(...)`.
- **Data source (verified live):** base `https://data.tg.ch/`, path `api/v2/catalog/datasets/sk-stat-4/records`, query `?limit={<=100}&offset={n}`. Response: `{ "total_count": int, "records": [ { "record": { "fields": { ... } } } ] }`. Field map: `bfs_nr_gemeinde` (string→int) → `BfsNumber`; `gemeinde_name` (string) → `MunicipalityName`; `jahr` (string→int) → `Year`; `selbstfinanzierungsgrad_in` (number, nullable) → `SelfFinancingRatio`; `nettoschuld_nettovermogen_pro_einwohner_in_chf` (number, nullable) → `NetDebtPerCapitaChf`. 480 records total. Ignore the other 7 ratios (they belong to follow-up #2).

**Branch:** create `feature/import-job` off `main` before Task 1. PR at the end like #1 (merge commit, not squash).

---

### Task 1: Idempotent upsert on the repository

Extend the repository port with a batch upsert keyed on `(BfsNumber, Year)` and implement it in EF. Because `MunicipalFinanceRecord` is immutable (get-only properties), the update path uses `Entry(existing).CurrentValues.SetValues(record)`. Existing rows are loaded once (no per-record query).

**Files:**
- Modify: `src/Kantonal.Application/IFinanceRepository.cs`
- Modify: `src/Kantonal.Infrastructure/EfFinanceRepository.cs`
- Test: `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`

**Interfaces:**
- Consumes: `MunicipalFinanceRecord(BfsNumber, string, int, decimal?, decimal?)`, `BfsNumber.Create(int)`, `KantonalDbContext.FinanceRecords` (all existing).
- Produces: `Task<int> IFinanceRepository.UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)` — inserts new rows, updates changed ones keyed on `(BfsNumber, Year)`, returns the number of records processed.

- [ ] **Step 1: Write the failing test**

Add to `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs` (inside the existing class; `NewContext()` already exists):

```csharp
    [Fact]
    public async Task UpsertManyAsync_InsertsNewAndUpdatesExisting()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.Add(
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, 163.81m, 1415.95m));
        await ctx.SaveChangesAsync();

        var repo = new EfFinanceRepository(ctx);
        var affected = await repo.UpsertManyAsync(new[]
        {
            // same key (4551, 2024) -> update
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, 200.00m, 999.99m),
            // new key -> insert
            new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, 80.36m, -683.62m),
        }, CancellationToken.None);

        Assert.Equal(2, affected);

        var all = await ctx.FinanceRecords.OrderBy(r => r.MunicipalityName).ToListAsync();
        Assert.Equal(2, all.Count);

        var aadorf = all.Single(r => r.BfsNumber == BfsNumber.Create(4551));
        Assert.Equal(200.00m, aadorf.SelfFinancingRatio);
        Assert.Equal(999.99m, aadorf.NetDebtPerCapitaChf);

        Assert.Contains(all, r => r.MunicipalityName == "Affeltrangen");
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter UpsertManyAsync_InsertsNewAndUpdatesExisting`
Expected: FAIL — compile error, `IFinanceRepository`/`EfFinanceRepository` has no `UpsertManyAsync`.

- [ ] **Step 3: Add the port method**

In `src/Kantonal.Application/IFinanceRepository.cs`, add inside the interface:

```csharp
    Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct);
```

- [ ] **Step 4: Implement it in EF**

In `src/Kantonal.Infrastructure/EfFinanceRepository.cs`, add this method to the class:

```csharp
    public async Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return 0;

        // Load existing rows once, then partition in memory — no query inside the loop.
        var existing = await _db.FinanceRecords.ToListAsync(ct);
        var byKey = existing.ToDictionary(r => (r.BfsNumber, r.Year));

        foreach (var record in records)
        {
            if (byKey.TryGetValue((record.BfsNumber, record.Year), out var current))
                _db.Entry(current).CurrentValues.SetValues(record);
            else
                _db.FinanceRecords.Add(record);
        }

        await _db.SaveChangesAsync(ct);
        return records.Count;
    }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter UpsertManyAsync_InsertsNewAndUpdatesExisting`
Expected: PASS.

- [ ] **Step 6: Run the full suite (no regressions)**

Run: `dotnet test`
Expected: PASS (13/13 — the 12 existing plus this one).

- [ ] **Step 7: Commit**

```bash
git add src/Kantonal.Application/IFinanceRepository.cs src/Kantonal.Infrastructure/EfFinanceRepository.cs tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs
git commit -m "feat(infrastructure): idempotent UpsertManyAsync on finance repository"
```

---

### Task 2: Import source port and orchestration service

Add the `IFinanceImportSource` port (fetch all domain records) and a `FinanceImportService` that fetches then upserts. **DI registration is deferred to Task 4**: registering `FinanceImportService` here would leave the DI graph with an unresolvable `IFinanceImportSource` (no implementation until Task 4), and `WebApplicationFactory` validates the service graph on build — breaking the existing `FinanceEndpointTests`. So Task 2 ships the classes + unit test only; Task 4 registers `FinanceImportService` and the import source together.

**Files:**
- Create: `src/Kantonal.Application/IFinanceImportSource.cs`
- Create: `src/Kantonal.Application/FinanceImportService.cs`
- Test: `tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs`

**Interfaces:**
- Consumes: `IFinanceRepository.UpsertManyAsync(...)` (Task 1), `MunicipalFinanceRecord`.
- Produces:
  - `IFinanceImportSource.FetchAllAsync(CancellationToken ct) -> Task<IReadOnlyList<MunicipalFinanceRecord>>`
  - `FinanceImportService(IFinanceImportSource source, IFinanceRepository repository)` with `Task<int> ImportAsync(CancellationToken ct)` returning the number of records upserted.

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs`:

```csharp
using Kantonal.Application;
using Kantonal.Domain;

namespace Kantonal.Tests.Application;

public class FinanceImportServiceTests
{
    [Fact]
    public async Task ImportAsync_UpsertsAllFetchedRecords()
    {
        var source = new StubSource(
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, 163.81m, 1415.95m),
            new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, 80.36m, -683.62m));
        var repo = new RecordingRepository();
        var service = new FinanceImportService(source, repo);

        var imported = await service.ImportAsync(CancellationToken.None);

        Assert.Equal(2, imported);
        Assert.NotNull(repo.Upserted);
        Assert.Equal(2, repo.Upserted!.Count);
        Assert.Contains(repo.Upserted, r => r.MunicipalityName == "Aadorf");
    }

    private sealed class StubSource : IFinanceImportSource
    {
        private readonly IReadOnlyList<MunicipalFinanceRecord> _records;
        public StubSource(params MunicipalFinanceRecord[] records) => _records = records;
        public Task<IReadOnlyList<MunicipalFinanceRecord>> FetchAllAsync(CancellationToken ct)
            => Task.FromResult(_records);
    }

    private sealed class RecordingRepository : IFinanceRepository
    {
        public IReadOnlyList<MunicipalFinanceRecord>? Upserted { get; private set; }

        public Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
        {
            Upserted = records;
            return Task.FromResult(records.Count);
        }

        public Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(Array.Empty<MunicipalFinanceRecord>());

        public Task<int> CountAsync(CancellationToken ct) => Task.FromResult(0);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter ImportAsync_UpsertsAllFetchedRecords`
Expected: FAIL — `IFinanceImportSource` and `FinanceImportService` do not exist.

- [ ] **Step 3: Create the port**

Create `src/Kantonal.Application/IFinanceImportSource.cs`:

```csharp
using Kantonal.Domain;

namespace Kantonal.Application;

/// <summary>Fetches municipal finance records from an external source (e.g. opendata.swiss).</summary>
public interface IFinanceImportSource
{
    Task<IReadOnlyList<MunicipalFinanceRecord>> FetchAllAsync(CancellationToken ct);
}
```

- [ ] **Step 4: Create the service**

Create `src/Kantonal.Application/FinanceImportService.cs`:

```csharp
namespace Kantonal.Application;

/// <summary>Imports finance records: fetch from the source, then upsert into the repository.</summary>
public sealed class FinanceImportService
{
    private readonly IFinanceImportSource _source;
    private readonly IFinanceRepository _repository;

    public FinanceImportService(IFinanceImportSource source, IFinanceRepository repository)
    {
        _source = source;
        _repository = repository;
    }

    public async Task<int> ImportAsync(CancellationToken ct)
    {
        var records = await _source.FetchAllAsync(ct);
        return await _repository.UpsertManyAsync(records, ct);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter ImportAsync_UpsertsAllFetchedRecords`
Expected: PASS.

Do NOT register `FinanceImportService` in DI here — that is Task 4's job (see the task description above for why). Do not create any stub/placeholder implementation of `IFinanceImportSource`; the real one arrives in Task 3 and is wired in Task 4.

- [ ] **Step 6: Run the full suite (no regressions)**

Run: `dotnet test`
Expected: PASS (14/14 — the 13 so far plus this one; `FinanceEndpointTests` stays green because nothing resolves `FinanceImportService` yet).

- [ ] **Step 7: Commit**

```bash
git add src/Kantonal.Application/IFinanceImportSource.cs src/Kantonal.Application/FinanceImportService.cs tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs
git commit -m "feat(application): finance import source port and orchestration service"
```

---

### Task 3: Thurgau opendata.swiss importer

Implement `ThurgauFinanceImporter : IFinanceImportSource` — a typed `HttpClient` that pages the records API and maps JSON `fields` → domain. Robust parsing: string ints, nullable ratios, skip rows that can't yield a valid key/name. Page size is a constructor parameter (default 100) so tests can drive pagination with a small page. All tests use a stubbed `HttpMessageHandler` — no live call.

**Files:**
- Create: `src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs`
- Test: `tests/Kantonal.Tests/Infrastructure/ThurgauFinanceImporterTests.cs`

**Interfaces:**
- Consumes: `IFinanceImportSource` (Task 2), `MunicipalFinanceRecord`, `BfsNumber.Create(int)`.
- Produces: `ThurgauFinanceImporter(HttpClient http, int pageSize = 100)` implementing `FetchAllAsync`. Expects `http.BaseAddress` to be set to `https://data.tg.ch/` by the caller. Requests `api/v2/catalog/datasets/sk-stat-4/records?limit={pageSize}&offset={n}` and stops once `offset >= total_count`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Kantonal.Tests/Infrastructure/ThurgauFinanceImporterTests.cs`:

```csharp
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
          "total_count": 2,
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
            $$"""{ "record": { "fields": { "bfs_nr_gemeinde": "{{r.Bfs}}", "gemeinde_name": "{{r.Name}}", "jahr": "{{r.Year}}" }}}"""));
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Kantonal.Tests --filter ThurgauFinanceImporterTests`
Expected: FAIL — `ThurgauFinanceImporter` does not exist.

- [ ] **Step 3: Implement the importer**

Create `src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Kantonal.Tests --filter ThurgauFinanceImporterTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs tests/Kantonal.Tests/Infrastructure/ThurgauFinanceImporterTests.cs
git commit -m "feat(infrastructure): Thurgau opendata.swiss finance importer with paging"
```

---

### Task 4: Wire the importer into the Api and retire the seeder

Register the typed `HttpClient`, run the import at startup (failure-tolerant so an outage never crashes the app), add a manual `POST /api/import` trigger, point the contract test at a fake source (offline + deterministic), delete `DatabaseSeeder`, and update the README.

**Files:**
- Modify: `src/Kantonal.Api/Program.cs`
- Delete: `src/Kantonal.Api/DatabaseSeeder.cs`
- Modify: `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `FinanceImportService.ImportAsync` (Task 2), `ThurgauFinanceImporter` (Task 3), `IFinanceImportSource` (Task 2), `ApiEnvelope.Success` (existing).
- Produces: `POST /api/import` → `{ ok: true, data: { imported: <int> } }`. A test-only `FakeFinanceImportSource` returning the three reference rows.

- [ ] **Step 1: Update the contract test to fake the source (failing)**

In `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`, add these usings at the top:

```csharp
using Kantonal.Application;
using Kantonal.Domain;
```

Replace the `TestApi` class body's `ConfigureServices` lambda so it also swaps the import source, and add a fake source class. The full replacement for the `TestApi` class:

```csharp
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
                new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, 163.81m, 1415.95m),
                new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, 80.36m, -683.62m),
                new MunicipalFinanceRecord(BfsNumber.Create(4486), "Amlikon-Bissegg", 2024, 95.10m, 210.40m),
            });
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter GetFinance_ReturnsOkEnvelopeWithItems`
Expected: FAIL — `IFinanceImportSource` is not yet registered in `Program` (no descriptor to remove and nothing populates the InMemory DB), so the assertion `Total >= 3` fails (or the app has no import wired yet).

- [ ] **Step 3: Wire the importer in `Program.cs`**

In `src/Kantonal.Api/Program.cs`:

1. Remove `using Kantonal.Api;` only if nothing else needs it — `ApiEnvelope` is in `Kantonal.Api`, and `Program.cs` lives in that namespace's assembly, so the `using` is not required for `ApiEnvelope`. Keep the file's existing usings except drop the seeder reference. After edits the usings block is:

```csharp
using Kantonal.Application;
using Kantonal.Infrastructure;
using Microsoft.EntityFrameworkCore;
```

2. After `builder.Services.AddInfrastructure(connectionString);`, register the typed client **and the import service** (its DI registration was deferred from Task 2 to here, so its `IFinanceImportSource` dependency resolves):

```csharp
builder.Services.AddHttpClient<IFinanceImportSource, ThurgauFinanceImporter>(client =>
{
    client.BaseAddress = new Uri("https://data.tg.ch/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<FinanceImportService>();
```

3. Add the manual trigger endpoint next to the other `Map*` calls (after the `/health` mapping):

```csharp
// Dev-only manual import trigger. No auth yet — authorization is a follow-up (see PROJECT_BRAINSTORM.md).
app.MapPost("/api/import", async (FinanceImportService importer, CancellationToken ct) =>
{
    var imported = await importer.ImportAsync(ct);
    return Results.Ok(ApiEnvelope.Success(new { imported }));
});
```

4. Replace the startup `using (var scope ...)` block (the one that calls `DatabaseSeeder.SeedAsync`) with a failure-tolerant import:

```csharp
// Apply migrations (relational only) and import finance data. A failed import must not crash startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KantonalDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();

    try
    {
        var importer = scope.ServiceProvider.GetRequiredService<FinanceImportService>();
        var imported = await importer.ImportAsync(CancellationToken.None);
        app.Logger.LogInformation("Startup finance import upserted {Count} records.", imported);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Startup finance import failed; continuing without fresh data.");
    }
}
```

- [ ] **Step 4: Delete the seeder**

```bash
git rm src/Kantonal.Api/DatabaseSeeder.cs
```

- [ ] **Step 5: Run the contract test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter GetFinance_ReturnsOkEnvelopeWithItems`
Expected: PASS — the fake source populates the InMemory DB at startup; Aadorf is present with `163.81 / 1415.95`.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test`
Expected: PASS (all green: the 12 originals + Task 1 + Task 2 + 3 from Task 3 = 17).

- [ ] **Step 7: Update the README**

In `README.md`, there is no dedicated data section yet. Add a new `## Data` section just before `## Architecture` (around line 25), and amend the follow-up line at `README.md:31` — change `Azure deployment notes and the opendata.swiss import job are tracked in follow-up work.` to `Azure deployment notes are tracked in follow-up work.` (drop the import-job clause, since it now ships). The new section to add:

```markdown
## Data

Finance data is imported from the Kanton Thurgau open-data portal
(opendata.swiss dataset `sk-stat-4`, served via the Opendatasoft records API at
`https://data.tg.ch/`). The API imports all records at startup (failure-tolerant:
an outage logs an error and the app still starts) and on demand via:

    POST /api/import   ->   { "ok": true, "data": { "imported": <count> } }

The import is an idempotent upsert keyed on `(BfsNumber, Year)`, so it is safe to
re-run. Only two KPI ratios are modelled today (self-financing ratio, net debt per
capita); the remaining ratios are a planned follow-up.
```

Confirm there is no longer any mention of a static 3-row seeder anywhere in the README.

- [ ] **Step 8: Verify the build and formatting**

Run: `dotnet build` then `dotnet format --verify-no-changes`
Expected: build succeeds; format reports no changes (run `dotnet format` and re-stage if it does).

- [ ] **Step 9: Commit**

```bash
git add src/Kantonal.Api/Program.cs tests/Kantonal.Tests/Api/FinanceEndpointTests.cs README.md
git commit -m "feat(api): import live Thurgau finance data at startup and via POST /api/import"
```

---

## Final verification (after all tasks)

- [ ] `dotnet test` — all green.
- [ ] `dotnet build` — no warnings introduced.
- [ ] Manual smoke (optional, needs Docker + network): `docker compose up`, then `curl -s localhost:8080/api/finance?pageSize=5` returns real municipalities, and `curl -s -X POST localhost:8080/api/import` returns `{ "ok": true, "data": { "imported": 480 } }`.
- [ ] Open a PR to `main` (merge commit, not squash) mirroring PR #1; body has summary + test plan.
- [ ] Update `HANDOFF.md` / the plan's follow-up list: mark follow-up #1 done, follow-up #2 (all 9 ratios) next.

## Self-review notes (author)

- **Spec coverage:** typed HttpClient with explicit timeout (Task 3 + Task 4 §3.2) ✓; idempotent upsert on `(BfsNumber, Year)` (Task 1) ✓; robust parsing of string ints + nullable ratios (Task 3) ✓; runs at startup + manual trigger (Task 4) ✓; mapper unit-tested against captured payload with stub handler (Task 3) ✓; upsert tested on InMemory (Task 1) ✓; no live network in suite (fake source in Task 4, stub handler in Task 3) ✓; README updated (Task 4) ✓.
- **Type consistency:** `UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord>, CancellationToken) -> Task<int>` and `FetchAllAsync(CancellationToken) -> Task<IReadOnlyList<MunicipalFinanceRecord>>` used identically across tasks.
- **Assumption that could be wrong:** the Opendatasoft response shape (`records[].record.fields`) and field names are current (verified live 2026-06-26). If the portal changes the schema, Task 3's DTOs and the captured-payload test are the single point to update.
