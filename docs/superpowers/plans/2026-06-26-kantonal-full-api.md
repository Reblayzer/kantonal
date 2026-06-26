# Kantonal Full API + Domain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the Kantonal REST API — model all 9 HRM2 financial indicators, add filtering + sorting + a single-record endpoint, and map typed errors to HTTP via the existing `{ ok, … }` envelope.

**Architecture:** The 9 ratios are grouped into a `FinanceIndicators` value object used by the domain ctor, the DTO, and the importer; the entity stores them as flat scalar columns and materializes via a **private scalar constructor** (EF cannot bind a value object to a ctor parameter — verified by spike). Filtering/sorting/paging push into EF/SQL. Typed `NotFoundException`/`ValidationException` are mapped to HTTP by a global `IExceptionHandler`.

**Tech Stack:** .NET 8, ASP.NET Core minimal API, EF Core 8 (Npgsql + InMemory for tests), xUnit.

## Global Constraints

- **Clean architecture / dependency rule:** Domain ← Application ← Infrastructure/Api. Errors and query types live in `Kantonal.Application`; EF lives in `Kantonal.Infrastructure`; composition in `Kantonal.Api`. Application never references Infrastructure.
- **No invented metrics:** the 7 new ratios use conservative English names (*grad*→`Ratio`, *anteil*→`Share`, *quotient*→`Quotient`) and each property has an XML doc comment citing its **exact German source field**. No invented prose about financial meaning.
- **Entity mapping (verified):** EF 8.0.8 cannot bind a `ComplexProperty`/`OwnsOne` value object to a constructor parameter. The entity stores the 9 ratios as **flat scalar get-only properties**, materializes via a **private scalar ctor** (EF binds private ctors), exposes a public `(BfsNumber, name, year, FinanceIndicators)` ctor delegating to it, and exposes `Indicators` as an `Ignore`d computed grouping.
- **Queries in SQL, never in memory:** filter/sort/paginate via EF `IQueryable`. Case-insensitive municipality match uses `MunicipalityName.ToLower().Contains(x.ToLower())` (translatable on Npgsql **and** evaluable on InMemory — do NOT use `EF.Functions.ILike`, which InMemory can't run). Ratio sorts are **nulls-last** regardless of direction via an explicit `OrderBy(r => r.X == null)` first key.
- **API envelope:** success `ApiEnvelope.Success(data)` → `{ ok:true, data }`; error `ApiEnvelope.Error(code, message)` → `{ ok:false, error:{ code, message } }`. Errors mapped to HTTP in the handler, not the service.
- **Page sizing:** `page` floored to 1; `pageSize` clamped 1..100 (`MaxPageSize = 100`).
- **TDD:** failing test first, minimal code to green, commit per task. Stage specific files only (never `git add .`). Conventional commits with scope.
- **The 9 ratios — canonical names (use these EXACT spellings everywhere):**

  | German source field | C# property |
  |---|---|
  | `selbstfinanzierungsgrad_in` | `SelfFinancingRatio` |
  | `selbstfinanzierungsanteil_in` | `SelfFinancingShare` |
  | `zinsbelastungsanteil_in` | `InterestBurdenShare` |
  | `kapitaldienstanteil_in` | `CapitalServiceShare` |
  | `investitionsanteil_in` | `InvestmentShare` |
  | `bruttoverschuldungsanteil_in` | `GrossDebtShare` |
  | `nettoschuld_nettovermogen_pro_einwohner_in_chf` | `NetDebtPerCapitaChf` |
  | `nettoverschuldungsquotient_in` | `NetDebtQuotient` |
  | `bilanzuberschussquotient_in` | `BalanceSheetSurplusQuotient` |

**Branch:** `feature/full-api` (already created off `main`). PR at the end like #1/#2 (merge commit).

---

### Task 1: `FinanceIndicators` value object + entity refactor + DTO

Introduce the value object, switch the entity to the dual-ctor shape with all 9 flat ratio properties, expand the DTO + `ToDto`, and adapt every existing construction site so the build stays green. No new columns/migration yet (Task 2); EF auto-maps the new scalar properties for InMemory, and `Indicators` is ignored.

**Files:**
- Create: `src/Kantonal.Domain/FinanceIndicators.cs`
- Modify: `src/Kantonal.Domain/MunicipalFinanceRecord.cs`
- Modify: `src/Kantonal.Application/FinanceRecordDto.cs`
- Modify: `src/Kantonal.Application/FinanceQueryService.cs` (the `ToDto` mapper only)
- Modify: `src/Kantonal.Infrastructure/KantonalDbContext.cs` (add `entity.Ignore(e => e.Indicators);`)
- Modify: `src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs` (adapt the `new MunicipalFinanceRecord(...)` call)
- Modify (construction sites in tests): `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`, `tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs`, `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`
- Test: `tests/Kantonal.Tests/Domain/MunicipalFinanceRecordTests.cs`

**Interfaces:**
- Produces:
  - `FinanceIndicators(decimal? SelfFinancingRatio, decimal? SelfFinancingShare, decimal? InterestBurdenShare, decimal? CapitalServiceShare, decimal? InvestmentShare, decimal? GrossDebtShare, decimal? NetDebtPerCapitaChf, decimal? NetDebtQuotient, decimal? BalanceSheetSurplusQuotient)` — a record (value equality).
  - `MunicipalFinanceRecord(BfsNumber bfsNumber, string municipalityName, int year, FinanceIndicators indicators)` — public domain ctor.
  - `MunicipalFinanceRecord` exposes get-only `BfsNumber`, `MunicipalityName`, `Year`, the 9 ratio properties, and computed `FinanceIndicators Indicators`.
  - `FinanceRecordDto(int BfsNumber, string MunicipalityName, int Year, decimal? SelfFinancingRatio, decimal? SelfFinancingShare, decimal? InterestBurdenShare, decimal? CapitalServiceShare, decimal? InvestmentShare, decimal? GrossDebtShare, decimal? NetDebtPerCapitaChf, decimal? NetDebtQuotient, decimal? BalanceSheetSurplusQuotient)`.

- [ ] **Step 1: Write the failing test**

Replace the body of `tests/Kantonal.Tests/Domain/MunicipalFinanceRecordTests.cs` with (keep any existing namespace/usings for `Kantonal.Domain`):

```csharp
using Kantonal.Domain;

namespace Kantonal.Tests.Domain;

public class MunicipalFinanceRecordTests
{
    private static FinanceIndicators SampleIndicators() => new(
        SelfFinancingRatio: 163.81m, SelfFinancingShare: 20.20m, InterestBurdenShare: 0.63m,
        CapitalServiceShare: 6.81m, InvestmentShare: 14.01m, GrossDebtShare: 141.04m,
        NetDebtPerCapitaChf: 1415.95m, NetDebtQuotient: 105.81m, BalanceSheetSurplusQuotient: 128.37m);

    [Fact]
    public void Constructor_ExposesIndicatorsAndScalars()
    {
        var record = new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, SampleIndicators());

        Assert.Equal(4551, record.BfsNumber.Value);
        Assert.Equal("Aadorf", record.MunicipalityName);
        Assert.Equal(2024, record.Year);
        Assert.Equal(163.81m, record.SelfFinancingRatio);
        Assert.Equal(128.37m, record.BalanceSheetSurplusQuotient);
        // computed grouping round-trips back to an equal value object
        Assert.Equal(SampleIndicators(), record.Indicators);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankName(string name)
        => Assert.Throws<ArgumentException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(4551), name, 2024, SampleIndicators()));

    [Fact]
    public void Constructor_RejectsYearBefore1900()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 1899, SampleIndicators()));

    [Fact]
    public void Indicators_HasValueEquality()
        => Assert.Equal(SampleIndicators(), SampleIndicators());
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter MunicipalFinanceRecordTests`
Expected: FAIL — `FinanceIndicators` does not exist; the 4-arg ctor does not exist.

- [ ] **Step 3: Create `FinanceIndicators`**

Create `src/Kantonal.Domain/FinanceIndicators.cs`:

```csharp
namespace Kantonal.Domain;

/// <summary>
/// The nine HRM2 financial key figures for a municipality-year, as published by the
/// Kanton Thurgau open-data portal (dataset sk-stat-4). A value object — value equality,
/// no identity. Each property documents its exact source field; all are nullable because
/// the source may omit any of them.
/// </summary>
public sealed record FinanceIndicators(
    /// <summary>Source: <c>selbstfinanzierungsgrad_in</c>.</summary>
    decimal? SelfFinancingRatio,
    /// <summary>Source: <c>selbstfinanzierungsanteil_in</c>.</summary>
    decimal? SelfFinancingShare,
    /// <summary>Source: <c>zinsbelastungsanteil_in</c>.</summary>
    decimal? InterestBurdenShare,
    /// <summary>Source: <c>kapitaldienstanteil_in</c>.</summary>
    decimal? CapitalServiceShare,
    /// <summary>Source: <c>investitionsanteil_in</c>.</summary>
    decimal? InvestmentShare,
    /// <summary>Source: <c>bruttoverschuldungsanteil_in</c>.</summary>
    decimal? GrossDebtShare,
    /// <summary>Source: <c>nettoschuld_nettovermogen_pro_einwohner_in_chf</c>.</summary>
    decimal? NetDebtPerCapitaChf,
    /// <summary>Source: <c>nettoverschuldungsquotient_in</c>.</summary>
    decimal? NetDebtQuotient,
    /// <summary>Source: <c>bilanzuberschussquotient_in</c>.</summary>
    decimal? BalanceSheetSurplusQuotient);
```

- [ ] **Step 4: Refactor `MunicipalFinanceRecord`**

Replace the contents of `src/Kantonal.Domain/MunicipalFinanceRecord.cs`:

```csharp
namespace Kantonal.Domain;

public sealed class MunicipalFinanceRecord
{
    // EF Core materializes through this private scalar constructor (it cannot bind a
    // value object to a ctor parameter). Domain code uses the public ctor below.
    private MunicipalFinanceRecord(
        BfsNumber bfsNumber,
        string municipalityName,
        int year,
        decimal? selfFinancingRatio,
        decimal? selfFinancingShare,
        decimal? interestBurdenShare,
        decimal? capitalServiceShare,
        decimal? investmentShare,
        decimal? grossDebtShare,
        decimal? netDebtPerCapitaChf,
        decimal? netDebtQuotient,
        decimal? balanceSheetSurplusQuotient)
    {
        if (string.IsNullOrWhiteSpace(municipalityName))
            throw new ArgumentException("Municipality name is required.", nameof(municipalityName));
        if (year < 1900)
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be 1900 or later.");

        BfsNumber = bfsNumber;
        MunicipalityName = municipalityName.Trim();
        Year = year;
        SelfFinancingRatio = selfFinancingRatio;
        SelfFinancingShare = selfFinancingShare;
        InterestBurdenShare = interestBurdenShare;
        CapitalServiceShare = capitalServiceShare;
        InvestmentShare = investmentShare;
        GrossDebtShare = grossDebtShare;
        NetDebtPerCapitaChf = netDebtPerCapitaChf;
        NetDebtQuotient = netDebtQuotient;
        BalanceSheetSurplusQuotient = balanceSheetSurplusQuotient;
    }

    public MunicipalFinanceRecord(BfsNumber bfsNumber, string municipalityName, int year, FinanceIndicators indicators)
        : this(
            bfsNumber, municipalityName, year,
            (indicators ?? throw new ArgumentNullException(nameof(indicators))).SelfFinancingRatio,
            indicators.SelfFinancingShare,
            indicators.InterestBurdenShare,
            indicators.CapitalServiceShare,
            indicators.InvestmentShare,
            indicators.GrossDebtShare,
            indicators.NetDebtPerCapitaChf,
            indicators.NetDebtQuotient,
            indicators.BalanceSheetSurplusQuotient)
    {
    }

    public BfsNumber BfsNumber { get; }
    public string MunicipalityName { get; }
    public int Year { get; }

    public decimal? SelfFinancingRatio { get; }
    public decimal? SelfFinancingShare { get; }
    public decimal? InterestBurdenShare { get; }
    public decimal? CapitalServiceShare { get; }
    public decimal? InvestmentShare { get; }
    public decimal? GrossDebtShare { get; }
    public decimal? NetDebtPerCapitaChf { get; }
    public decimal? NetDebtQuotient { get; }
    public decimal? BalanceSheetSurplusQuotient { get; }

    /// <summary>The nine ratios as a value object. Not mapped (EF Ignores it); computed from the scalar columns.</summary>
    public FinanceIndicators Indicators => new(
        SelfFinancingRatio, SelfFinancingShare, InterestBurdenShare, CapitalServiceShare,
        InvestmentShare, GrossDebtShare, NetDebtPerCapitaChf, NetDebtQuotient, BalanceSheetSurplusQuotient);
}
```

- [ ] **Step 5: Ignore `Indicators` in the DbContext**

In `src/Kantonal.Infrastructure/KantonalDbContext.cs`, inside `OnModelCreating`, after the existing property mappings and before the closing brace of the method, add:

```csharp
        entity.Ignore(e => e.Indicators);
```

(The 7 new scalar properties are auto-mapped by EF convention for now; Task 2 adds explicit column names + a migration.)

- [ ] **Step 6: Expand the DTO and `ToDto`**

Replace `src/Kantonal.Application/FinanceRecordDto.cs`:

```csharp
namespace Kantonal.Application;

public record FinanceRecordDto(
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
```

In `src/Kantonal.Application/FinanceQueryService.cs`, replace the `ToDto` method:

```csharp
    private static FinanceRecordDto ToDto(MunicipalFinanceRecord r) => new(
        r.BfsNumber.Value, r.MunicipalityName, r.Year,
        r.SelfFinancingRatio, r.SelfFinancingShare, r.InterestBurdenShare, r.CapitalServiceShare,
        r.InvestmentShare, r.GrossDebtShare, r.NetDebtPerCapitaChf, r.NetDebtQuotient, r.BalanceSheetSurplusQuotient);
```

- [ ] **Step 7: Adapt the importer construction site**

In `src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs`, change the `Map` method's record construction (it currently builds with two ratios) to build a `FinanceIndicators` with the two values it has today and the other seven `null` (Task 3 fills them):

```csharp
    private static MunicipalFinanceRecord? Map(FinanceFields fields)
    {
        if (!int.TryParse(fields.BfsNumber, out var bfs)) return null;
        if (!int.TryParse(fields.Year, out var year)) return null;
        if (string.IsNullOrWhiteSpace(fields.MunicipalityName)) return null;

        var indicators = new FinanceIndicators(
            ToDecimal(fields.SelfFinancingRatio), null, null, null, null, null,
            ToDecimal(fields.NetDebtPerCapitaChf), null, null);

        return new MunicipalFinanceRecord(BfsNumber.Create(bfs), fields.MunicipalityName, year, indicators);
    }
```

Ensure `using Kantonal.Domain;` is present (it already is).

- [ ] **Step 8: Adapt test construction sites**

These three test files construct records with the old `(bfs, name, year, sfr, ndpc)` ctor. Update each construction to use `FinanceIndicators`. A helper keeps it terse — in **each** of the three files add a private static helper to the test class and replace the constructions:

In `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`, replace each `new MunicipalFinanceRecord(BfsNumber.Create(N), "Name", YEAR, A, B)` with `new MunicipalFinanceRecord(BfsNumber.Create(N), "Name", YEAR, Ind(A, B))` and add:

```csharp
    private static FinanceIndicators Ind(decimal? selfFinancing, decimal? netDebt) =>
        new(selfFinancing, null, null, null, null, null, netDebt, null, null);
```

Do the identical replacement + helper in `tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs` (the `StubSource` records) and in `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs` (the `FakeFinanceImportSource` rows). Each file needs `using Kantonal.Domain;` (add if missing).

- [ ] **Step 9: Run the full suite**

Run: `dotnet test`
Expected: PASS — all green (the existing 19 adapted + the new domain tests). Confirm no `MunicipalFinanceRecord(` call uses the removed 5-arg ctor (a leftover would be a compile error).

- [ ] **Step 10: Commit**

```bash
git add src/Kantonal.Domain/FinanceIndicators.cs src/Kantonal.Domain/MunicipalFinanceRecord.cs src/Kantonal.Application/FinanceRecordDto.cs src/Kantonal.Application/FinanceQueryService.cs src/Kantonal.Infrastructure/KantonalDbContext.cs src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs tests/Kantonal.Tests/Domain/MunicipalFinanceRecordTests.cs tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs tests/Kantonal.Tests/Application/FinanceImportServiceTests.cs tests/Kantonal.Tests/Api/FinanceEndpointTests.cs
git commit -m "feat(domain): FinanceIndicators value object and 9-ratio entity"
```

---

### Task 2: EF column mapping + migration for the 7 new ratios

Map the 7 new ratios to explicit snake_case `numeric` columns and add an EF migration. Verify via an InMemory round-trip that all 9 ratios persist (proving none is accidentally `Ignore`d).

**Files:**
- Modify: `src/Kantonal.Infrastructure/KantonalDbContext.cs`
- Create (generated): `src/Kantonal.Infrastructure/Migrations/<timestamp>_AddFinanceIndicators.cs` (+ `.Designer.cs`, + updated `KantonalDbContextModelSnapshot.cs`)
- Test: `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`

**Interfaces:**
- Consumes: `MunicipalFinanceRecord` 9 properties, `FinanceIndicators` (Task 1).
- Produces: columns `self_financing_share`, `interest_burden_share`, `capital_service_share`, `investment_share`, `gross_debt_share`, `net_debt_quotient`, `balance_sheet_surplus_quotient` on `finance_records`.

- [ ] **Step 1: Write the failing test**

Add to `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`. This uses two contexts over the **same** named InMemory store (InMemory DBs are keyed by name) to prove the ratios survive a fresh context — i.e. they are mapped, not `Ignore`d:

```csharp
    [Fact]
    public async Task Upsert_PersistsAllNineRatios()
    {
        var opts = new DbContextOptionsBuilder<KantonalDbContext>()
            .UseInMemoryDatabase("nine-ratios").Options;
        var indicators = new FinanceIndicators(1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m);

        await using (var ctx = new KantonalDbContext(opts))
        {
            var repo = new EfFinanceRepository(ctx);
            await repo.UpsertManyAsync(new[]
            {
                new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, indicators)
            }, CancellationToken.None);
        }

        await using (var verify = new KantonalDbContext(opts))
        {
            var loaded = await verify.FinanceRecords.SingleAsync();
            Assert.Equal(indicators, loaded.Indicators);
        }
    }
```

- [ ] **Step 2: Run the test to verify it fails or passes**

Run: `dotnet test tests/Kantonal.Tests --filter Upsert_PersistsAllNineRatios`
Expected: PASS already (Task 1's auto-mapped properties round-trip on InMemory). This test is a guard that the explicit mapping in Step 3 does not accidentally drop a column. If it FAILS, a property is mis-mapped — fix before continuing.

- [ ] **Step 3: Add explicit column mapping**

In `src/Kantonal.Infrastructure/KantonalDbContext.cs`, alongside the existing two ratio `Property` lines, add the seven new ones (keep the existing `self_financing_ratio` and `net_debt_per_capita_chf` lines unchanged):

```csharp
        entity.Property(e => e.SelfFinancingShare).HasColumnName("self_financing_share").HasColumnType("numeric");
        entity.Property(e => e.InterestBurdenShare).HasColumnName("interest_burden_share").HasColumnType("numeric");
        entity.Property(e => e.CapitalServiceShare).HasColumnName("capital_service_share").HasColumnType("numeric");
        entity.Property(e => e.InvestmentShare).HasColumnName("investment_share").HasColumnType("numeric");
        entity.Property(e => e.GrossDebtShare).HasColumnName("gross_debt_share").HasColumnType("numeric");
        entity.Property(e => e.NetDebtQuotient).HasColumnName("net_debt_quotient").HasColumnType("numeric");
        entity.Property(e => e.BalanceSheetSurplusQuotient).HasColumnName("balance_sheet_surplus_quotient").HasColumnType("numeric");
```

- [ ] **Step 4: Generate the migration**

Run (from repo root):

```bash
dotnet ef migrations add AddFinanceIndicators \
  --project src/Kantonal.Infrastructure --startup-project src/Kantonal.Api
```

Then open the generated `Migrations/<timestamp>_AddFinanceIndicators.cs` and verify `Up` calls `migrationBuilder.AddColumn<decimal>(...)` for exactly the seven new columns (names above) on table `finance_records`, all nullable, and `Down` drops them. Do not hand-edit beyond confirming. If `dotnet ef` is not installed: `dotnet tool install --global dotnet-ef` first.

- [ ] **Step 5: Run the test + full suite**

Run: `dotnet test tests/Kantonal.Tests --filter Upsert_PersistsAllNineRatios` then `dotnet test`
Expected: PASS (InMemory ignores column names, so this proves the properties are mapped, not Ignored). Also `dotnet build` succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/Kantonal.Infrastructure/KantonalDbContext.cs src/Kantonal.Infrastructure/Migrations/ tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs
git commit -m "feat(infrastructure): map and migrate the seven new finance ratios"
```

---

### Task 3: Importer maps all 9 source fields

Extend `FinanceFields` with the 7 new JSON fields and build a complete `FinanceIndicators` in `Map`.

**Files:**
- Modify: `src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs`
- Test: `tests/Kantonal.Tests/Infrastructure/ThurgauFinanceImporterTests.cs`

**Interfaces:**
- Consumes: `FinanceIndicators` (Task 1).
- Produces: importer now populates all 9 ratios from the source payload.

- [ ] **Step 1: Write the failing test**

Add to `tests/Kantonal.Tests/Infrastructure/ThurgauFinanceImporterTests.cs` (it already has `ImporterReturning` and the `StubHandler`):

```csharp
    [Fact]
    public async Task FetchAllAsync_MapsAllNineRatios()
    {
        const string payload = """
        {
          "total_count": 1,
          "records": [
            { "record": { "fields": {
              "bfs_nr_gemeinde": "4551",
              "gemeinde_name": "Aadorf",
              "jahr": "2024",
              "selbstfinanzierungsgrad_in": 163.81,
              "selbstfinanzierungsanteil_in": 20.20,
              "zinsbelastungsanteil_in": 0.63,
              "kapitaldienstanteil_in": 6.81,
              "investitionsanteil_in": 14.01,
              "bruttoverschuldungsanteil_in": 141.04,
              "nettoschuld_nettovermogen_pro_einwohner_in_chf": 1415.95,
              "nettoverschuldungsquotient_in": 105.81,
              "bilanzuberschussquotient_in": 128.37
            }}}
          ]
        }
        """;
        var importer = ImporterReturning(_ => payload, pageSize: 100);

        var records = await importer.FetchAllAsync(CancellationToken.None);

        var r = Assert.Single(records);
        Assert.Equal(20.20m, Math.Round(r.SelfFinancingShare!.Value, 2));
        Assert.Equal(0.63m, Math.Round(r.InterestBurdenShare!.Value, 2));
        Assert.Equal(6.81m, Math.Round(r.CapitalServiceShare!.Value, 2));
        Assert.Equal(14.01m, Math.Round(r.InvestmentShare!.Value, 2));
        Assert.Equal(141.04m, Math.Round(r.GrossDebtShare!.Value, 2));
        Assert.Equal(105.81m, Math.Round(r.NetDebtQuotient!.Value, 2));
        Assert.Equal(128.37m, Math.Round(r.BalanceSheetSurplusQuotient!.Value, 2));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter FetchAllAsync_MapsAllNineRatios`
Expected: FAIL — the new ratios are `null` (importer only maps two; `FinanceFields` lacks the new JSON fields).

- [ ] **Step 3: Extend `FinanceFields` and `Map`**

In `src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs`, replace the `FinanceFields` record with the full 9-field version:

```csharp
    private sealed record FinanceFields(
        [property: JsonPropertyName("bfs_nr_gemeinde")] string? BfsNumber,
        [property: JsonPropertyName("gemeinde_name")] string? MunicipalityName,
        [property: JsonPropertyName("jahr")] string? Year,
        [property: JsonPropertyName("selbstfinanzierungsgrad_in")] double? SelfFinancingRatio,
        [property: JsonPropertyName("selbstfinanzierungsanteil_in")] double? SelfFinancingShare,
        [property: JsonPropertyName("zinsbelastungsanteil_in")] double? InterestBurdenShare,
        [property: JsonPropertyName("kapitaldienstanteil_in")] double? CapitalServiceShare,
        [property: JsonPropertyName("investitionsanteil_in")] double? InvestmentShare,
        [property: JsonPropertyName("bruttoverschuldungsanteil_in")] double? GrossDebtShare,
        [property: JsonPropertyName("nettoschuld_nettovermogen_pro_einwohner_in_chf")] double? NetDebtPerCapitaChf,
        [property: JsonPropertyName("nettoverschuldungsquotient_in")] double? NetDebtQuotient,
        [property: JsonPropertyName("bilanzuberschussquotient_in")] double? BalanceSheetSurplusQuotient);
```

And replace the `indicators` construction in `Map` (from Task 1's two-value version) with the full mapping:

```csharp
        var indicators = new FinanceIndicators(
            ToDecimal(fields.SelfFinancingRatio),
            ToDecimal(fields.SelfFinancingShare),
            ToDecimal(fields.InterestBurdenShare),
            ToDecimal(fields.CapitalServiceShare),
            ToDecimal(fields.InvestmentShare),
            ToDecimal(fields.GrossDebtShare),
            ToDecimal(fields.NetDebtPerCapitaChf),
            ToDecimal(fields.NetDebtQuotient),
            ToDecimal(fields.BalanceSheetSurplusQuotient));
```

- [ ] **Step 4: Run the test + full suite**

Run: `dotnet test tests/Kantonal.Tests --filter ThurgauFinanceImporterTests` then `dotnet test`
Expected: PASS (the new test plus the existing importer tests — null-tolerance and paging still hold).

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Infrastructure/ThurgauFinanceImporter.cs tests/Kantonal.Tests/Infrastructure/ThurgauFinanceImporterTests.cs
git commit -m "feat(infrastructure): importer maps all nine finance ratios"
```

---

### Task 4: Repository query capability (filter, sort, single-record)

Introduce `FinanceQuery` + the sort enums, change `IFinanceRepository` to a query-based surface, implement it in EF (filter + nulls-last sort + paging), and adapt `FinanceQueryService` + the test `FakeRepo` to the new port so the build stays green. Service-level filter/sort/validation comes in Task 5.

**Files:**
- Create: `src/Kantonal.Application/FinanceQuery.cs`
- Modify: `src/Kantonal.Application/IFinanceRepository.cs`
- Modify: `src/Kantonal.Infrastructure/EfFinanceRepository.cs`
- Modify: `src/Kantonal.Application/FinanceQueryService.cs` (adapt to new port; still paging-only behavior)
- Modify: `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs` (adapt the `FakeRepo`)
- Test: `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`

**Interfaces:**
- Produces:
  - `enum FinanceSortField { MunicipalityName, Year, SelfFinancingRatio, SelfFinancingShare, InterestBurdenShare, CapitalServiceShare, InvestmentShare, GrossDebtShare, NetDebtPerCapitaChf, NetDebtQuotient, BalanceSheetSurplusQuotient }`
  - `enum SortDirection { Asc, Desc }`
  - `record FinanceQuery(string? Municipality, int? Year, FinanceSortField SortBy, SortDirection Direction, int Skip, int Take)`
  - `IFinanceRepository`:
    - `Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct)`
    - `Task<int> CountAsync(string? municipality, int? year, CancellationToken ct)`
    - `Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct)`
    - `Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)` (unchanged)

- [ ] **Step 1: Write the failing tests**

Add to `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`:

```csharp
    private static MunicipalFinanceRecord Rec(int bfs, string name, int year, decimal? selfFinancing)
        => new(BfsNumber.Create(bfs), name, year,
            new FinanceIndicators(selfFinancing, null, null, null, null, null, null, null, null));

    [Fact]
    public async Task QueryAsync_FiltersByMunicipalitySubstring_CaseInsensitive()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Aadorf", 2024, 1m), Rec(2, "Affeltrangen", 2024, 2m), Rec(3, "Bürglen", 2024, 3m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var q = new FinanceQuery("aff", null, FinanceSortField.MunicipalityName, SortDirection.Asc, 0, 50);
        var result = await repo.QueryAsync(q, CancellationToken.None);
        var count = await repo.CountAsync("aff", null, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal("Affeltrangen", Assert.Single(result).MunicipalityName);
    }

    [Fact]
    public async Task QueryAsync_FiltersByYear()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(Rec(1, "Aadorf", 2023, 1m), Rec(1, "Aadorf", 2024, 2m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var result = await repo.QueryAsync(
            new FinanceQuery(null, 2023, FinanceSortField.Year, SortDirection.Asc, 0, 50), CancellationToken.None);

        Assert.Equal(2023, Assert.Single(result).Year);
    }

    [Fact]
    public async Task QueryAsync_SortsByRatioDescending_NullsLast()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            Rec(1, "Low", 2024, 10m), Rec(2, "High", 2024, 99m), Rec(3, "Null", 2024, null));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        var result = await repo.QueryAsync(
            new FinanceQuery(null, null, FinanceSortField.SelfFinancingRatio, SortDirection.Desc, 0, 50),
            CancellationToken.None);

        Assert.Equal(new[] { "High", "Low", "Null" }, result.Select(r => r.MunicipalityName).ToArray());
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsRecordOrNull()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.Add(Rec(4551, "Aadorf", 2024, 1m));
        await ctx.SaveChangesAsync();
        var repo = new EfFinanceRepository(ctx);

        Assert.NotNull(await repo.GetByKeyAsync(BfsNumber.Create(4551), 2024, CancellationToken.None));
        Assert.Null(await repo.GetByKeyAsync(BfsNumber.Create(9999), 2024, CancellationToken.None));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Kantonal.Tests --filter "QueryAsync_FiltersByMunicipalitySubstring_CaseInsensitive|QueryAsync_FiltersByYear|QueryAsync_SortsByRatioDescending_NullsLast|GetByKeyAsync_ReturnsRecordOrNull"`
Expected: FAIL — `FinanceQuery`, the enums, and the new repository methods don't exist.

- [ ] **Step 3: Create `FinanceQuery` + enums**

Create `src/Kantonal.Application/FinanceQuery.cs`:

```csharp
namespace Kantonal.Application;

public enum SortDirection { Asc, Desc }

public enum FinanceSortField
{
    MunicipalityName,
    Year,
    SelfFinancingRatio,
    SelfFinancingShare,
    InterestBurdenShare,
    CapitalServiceShare,
    InvestmentShare,
    GrossDebtShare,
    NetDebtPerCapitaChf,
    NetDebtQuotient,
    BalanceSheetSurplusQuotient,
}

public sealed record FinanceQuery(
    string? Municipality,
    int? Year,
    FinanceSortField SortBy,
    SortDirection Direction,
    int Skip,
    int Take);
```

- [ ] **Step 4: Update the repository port**

Replace the body of `src/Kantonal.Application/IFinanceRepository.cs`:

```csharp
using Kantonal.Domain;

namespace Kantonal.Application;

public interface IFinanceRepository
{
    Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct);
    Task<int> CountAsync(string? municipality, int? year, CancellationToken ct);
    Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct);
    Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct);
}
```

- [ ] **Step 5: Implement in EF**

Replace the read methods in `src/Kantonal.Infrastructure/EfFinanceRepository.cs` (keep `UpsertManyAsync` unchanged). The file should now read:

```csharp
using Kantonal.Application;
using Kantonal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Infrastructure;

public class EfFinanceRepository : IFinanceRepository
{
    private readonly KantonalDbContext _db;
    public EfFinanceRepository(KantonalDbContext db) => _db = db;

    public async Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct)
    {
        var q = ApplyFilters(_db.FinanceRecords.AsQueryable(), query.Municipality, query.Year);
        q = ApplySort(q, query.SortBy, query.Direction);
        return await q.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
    }

    public Task<int> CountAsync(string? municipality, int? year, CancellationToken ct)
        => ApplyFilters(_db.FinanceRecords.AsQueryable(), municipality, year).CountAsync(ct);

    public async Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct)
        => await _db.FinanceRecords.FirstOrDefaultAsync(r => r.BfsNumber == bfsNumber && r.Year == year, ct);

    public async Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return 0;

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

    private static IQueryable<MunicipalFinanceRecord> ApplyFilters(
        IQueryable<MunicipalFinanceRecord> q, string? municipality, int? year)
    {
        if (!string.IsNullOrWhiteSpace(municipality))
        {
            var needle = municipality.ToLower();
            q = q.Where(r => r.MunicipalityName.ToLower().Contains(needle));
        }
        if (year is not null)
            q = q.Where(r => r.Year == year);
        return q;
    }

    private static IQueryable<MunicipalFinanceRecord> ApplySort(
        IQueryable<MunicipalFinanceRecord> q, FinanceSortField sortBy, SortDirection direction)
    {
        var asc = direction == SortDirection.Asc;

        IOrderedQueryable<MunicipalFinanceRecord> ordered = sortBy switch
        {
            FinanceSortField.MunicipalityName => asc ? q.OrderBy(r => r.MunicipalityName) : q.OrderByDescending(r => r.MunicipalityName),
            FinanceSortField.Year => asc ? q.OrderBy(r => r.Year) : q.OrderByDescending(r => r.Year),
            FinanceSortField.SelfFinancingRatio => ByRatio(q, r => r.SelfFinancingRatio, asc),
            FinanceSortField.SelfFinancingShare => ByRatio(q, r => r.SelfFinancingShare, asc),
            FinanceSortField.InterestBurdenShare => ByRatio(q, r => r.InterestBurdenShare, asc),
            FinanceSortField.CapitalServiceShare => ByRatio(q, r => r.CapitalServiceShare, asc),
            FinanceSortField.InvestmentShare => ByRatio(q, r => r.InvestmentShare, asc),
            FinanceSortField.GrossDebtShare => ByRatio(q, r => r.GrossDebtShare, asc),
            FinanceSortField.NetDebtPerCapitaChf => ByRatio(q, r => r.NetDebtPerCapitaChf, asc),
            FinanceSortField.NetDebtQuotient => ByRatio(q, r => r.NetDebtQuotient, asc),
            FinanceSortField.BalanceSheetSurplusQuotient => ByRatio(q, r => r.BalanceSheetSurplusQuotient, asc),
            _ => q.OrderBy(r => r.MunicipalityName),
        };

        // Stable, deterministic tiebreak.
        return ordered.ThenBy(r => r.MunicipalityName).ThenBy(r => r.Year);
    }

    // Nulls always sort last (whether asc or desc) by ordering on "is null" first.
    private static IOrderedQueryable<MunicipalFinanceRecord> ByRatio(
        IQueryable<MunicipalFinanceRecord> q,
        System.Linq.Expressions.Expression<Func<MunicipalFinanceRecord, decimal?>> key,
        bool asc)
    {
        var nullsLast = q.OrderBy(NullnessOf(key));
        return asc ? nullsLast.ThenBy(key) : nullsLast.ThenByDescending(key);
    }

    // Builds `r => <key>(r) == null` from the value key expression.
    private static System.Linq.Expressions.Expression<Func<MunicipalFinanceRecord, bool>> NullnessOf(
        System.Linq.Expressions.Expression<Func<MunicipalFinanceRecord, decimal?>> key)
    {
        var isNull = System.Linq.Expressions.Expression.Equal(
            key.Body, System.Linq.Expressions.Expression.Constant(null, typeof(decimal?)));
        return System.Linq.Expressions.Expression.Lambda<Func<MunicipalFinanceRecord, bool>>(isNull, key.Parameters);
    }
}
```

- [ ] **Step 6: Adapt `FinanceQueryService` to the new port (paging-only for now)**

In `src/Kantonal.Application/FinanceQueryService.cs`, replace `GetPageAsync` so it builds a default `FinanceQuery` (no filters, default sort) — the filter/sort API arrives in Task 5:

```csharp
    public async Task<PagedResult<FinanceRecordDto>> GetPageAsync(int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = new FinanceQuery(null, null, FinanceSortField.MunicipalityName, SortDirection.Asc,
            (page - 1) * pageSize, pageSize);

        var total = await _repo.CountAsync(null, null, ct);
        var records = await _repo.QueryAsync(query, ct);

        return new PagedResult<FinanceRecordDto>(records.Select(ToDto).ToList(), page, pageSize, total);
    }
```

- [ ] **Step 7: Adapt the test `FakeRepo`**

In `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs`, update the fake repository to implement the new port. Replace its method implementations so it stores a list and honours skip/take, e.g.:

```csharp
    private sealed class FakeRepo : IFinanceRepository
    {
        private readonly List<MunicipalFinanceRecord> _records;
        public FakeRepo(params MunicipalFinanceRecord[] records) => _records = records.ToList();

        public Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(
                _records.Skip(query.Skip).Take(query.Take).ToList());

        public Task<int> CountAsync(string? municipality, int? year, CancellationToken ct)
            => Task.FromResult(_records.Count);

        public Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct)
            => Task.FromResult(_records.FirstOrDefault(r => r.BfsNumber == bfsNumber && r.Year == year));

        public Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct)
            => Task.FromResult(records.Count);
    }
```

If the existing tests construct `FakeRepo` differently or assert on specific records, preserve those assertions — only the interface methods change. Keep using the `FinanceIndicators` construction helper from Task 1. Ensure `using Kantonal.Domain;` is present.

- [ ] **Step 8: Run the full suite**

Run: `dotnet test`
Expected: PASS — new repository tests green; `FinanceQueryServiceTests` green against the adapted fake; everything else unaffected.

- [ ] **Step 9: Commit**

```bash
git add src/Kantonal.Application/FinanceQuery.cs src/Kantonal.Application/IFinanceRepository.cs src/Kantonal.Infrastructure/EfFinanceRepository.cs src/Kantonal.Application/FinanceQueryService.cs tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs
git commit -m "feat(infrastructure): repository filtering, nulls-last sorting, single-record lookup"
```

---

### Task 5: Application query API + typed errors

Add `NotFoundException` + `ValidationException`, a `FinanceListRequest` input, and the service-level filter/sort/validation + single-record lookup. The service throws typed errors; HTTP mapping is Task 6.

**Files:**
- Create: `src/Kantonal.Application/Errors/NotFoundException.cs`
- Create: `src/Kantonal.Application/Errors/ValidationException.cs`
- Create: `src/Kantonal.Application/FinanceListRequest.cs`
- Modify: `src/Kantonal.Application/FinanceQueryService.cs`
- Test: `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs`

**Interfaces:**
- Produces:
  - `NotFoundException(string code, string message)` with `string Code`.
  - `ValidationException(string code, string message)` with `string Code`.
  - `record FinanceListRequest(string? Municipality, int? Year, string? SortBy, string? SortDir, int Page, int PageSize)`.
  - `FinanceQueryService.GetPageAsync(FinanceListRequest request, CancellationToken ct)` → `PagedResult<FinanceRecordDto>` (throws `ValidationException` on unknown `SortBy`/`SortDir`).
  - `FinanceQueryService.GetByKeyAsync(int bfsNumber, int year, CancellationToken ct)` → `FinanceRecordDto` (throws `NotFoundException` if absent, `ValidationException` if `bfsNumber <= 0`).

- [ ] **Step 1: Write the failing tests**

Add to `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs` (uses the `FakeRepo` from Task 4 and the `FinanceIndicators` helper from Task 1):

```csharp
    [Fact]
    public async Task GetPageAsync_RejectsUnknownSortField()
    {
        var service = new FinanceQueryService(new FakeRepo());
        var request = new FinanceListRequest(null, null, "not_a_field", null, 1, 20);

        await Assert.ThrowsAsync<Kantonal.Application.Errors.ValidationException>(
            () => service.GetPageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetPageAsync_RejectsUnknownSortDirection()
    {
        var service = new FinanceQueryService(new FakeRepo());
        var request = new FinanceListRequest(null, null, "year", "sideways", 1, 20);

        await Assert.ThrowsAsync<Kantonal.Application.Errors.ValidationException>(
            () => service.GetPageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetByKeyAsync_ThrowsNotFound_WhenAbsent()
    {
        var service = new FinanceQueryService(new FakeRepo());

        await Assert.ThrowsAsync<Kantonal.Application.Errors.NotFoundException>(
            () => service.GetByKeyAsync(9999, 2024, CancellationToken.None));
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsDto_WhenPresent()
    {
        var record = new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024,
            new FinanceIndicators(163.81m, null, null, null, null, null, 1415.95m, null, null));
        var service = new FinanceQueryService(new FakeRepo(record));

        var dto = await service.GetByKeyAsync(4551, 2024, CancellationToken.None);

        Assert.Equal("Aadorf", dto.MunicipalityName);
        Assert.Equal(163.81m, dto.SelfFinancingRatio);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Kantonal.Tests --filter "GetPageAsync_RejectsUnknownSortField|GetPageAsync_RejectsUnknownSortDirection|GetByKeyAsync_ThrowsNotFound_WhenAbsent|GetByKeyAsync_ReturnsDto_WhenPresent"`
Expected: FAIL — the error types, `FinanceListRequest`, and the new/overloaded service methods don't exist.

- [ ] **Step 3: Create the typed errors**

Create `src/Kantonal.Application/Errors/NotFoundException.cs`:

```csharp
namespace Kantonal.Application.Errors;

/// <summary>A requested resource does not exist. Mapped to HTTP 404 in the Api layer.</summary>
public sealed class NotFoundException : Exception
{
    public string Code { get; }
    public NotFoundException(string code, string message) : base(message) => Code = code;
}
```

Create `src/Kantonal.Application/Errors/ValidationException.cs`:

```csharp
namespace Kantonal.Application.Errors;

/// <summary>Caller input failed validation. Mapped to HTTP 400 in the Api layer.</summary>
public sealed class ValidationException : Exception
{
    public string Code { get; }
    public ValidationException(string code, string message) : base(message) => Code = code;
}
```

- [ ] **Step 4: Create `FinanceListRequest`**

Create `src/Kantonal.Application/FinanceListRequest.cs`:

```csharp
namespace Kantonal.Application;

public sealed record FinanceListRequest(
    string? Municipality,
    int? Year,
    string? SortBy,
    string? SortDir,
    int Page,
    int PageSize);
```

- [ ] **Step 5: Extend `FinanceQueryService`**

Replace `src/Kantonal.Application/FinanceQueryService.cs` with the full filter/sort/validation + single-record version:

```csharp
using Kantonal.Application.Errors;
using Kantonal.Domain;

namespace Kantonal.Application;

public sealed class FinanceQueryService
{
    private const int MaxPageSize = 100;
    private readonly IFinanceRepository _repo;

    public FinanceQueryService(IFinanceRepository repo) => _repo = repo;

    public async Task<PagedResult<FinanceRecordDto>> GetPageAsync(FinanceListRequest request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var sortBy = ParseSortField(request.SortBy);
        var direction = ParseDirection(request.SortDir);

        var query = new FinanceQuery(request.Municipality, request.Year, sortBy, direction,
            (page - 1) * pageSize, pageSize);

        var total = await _repo.CountAsync(request.Municipality, request.Year, ct);
        var records = await _repo.QueryAsync(query, ct);

        return new PagedResult<FinanceRecordDto>(records.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<FinanceRecordDto> GetByKeyAsync(int bfsNumber, int year, CancellationToken ct)
    {
        if (bfsNumber <= 0)
            throw new ValidationException("invalid_bfs", $"BFS number must be positive; got {bfsNumber}.");

        var record = await _repo.GetByKeyAsync(BfsNumber.Create(bfsNumber), year, ct);
        if (record is null)
            throw new NotFoundException("finance_record_not_found",
                $"No finance record for BFS {bfsNumber}, year {year}.");

        return ToDto(record);
    }

    private static FinanceSortField ParseSortField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FinanceSortField.MunicipalityName;
        if (Enum.TryParse<FinanceSortField>(value, ignoreCase: true, out var field) && Enum.IsDefined(field))
            return field;
        throw new ValidationException("invalid_sort_field", $"Unknown sortBy value '{value}'.");
    }

    private static SortDirection ParseDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SortDirection.Asc;
        if (Enum.TryParse<SortDirection>(value, ignoreCase: true, out var dir) && Enum.IsDefined(dir))
            return dir;
        throw new ValidationException("invalid_sort_dir", $"Unknown sortDir value '{value}'.");
    }

    private static FinanceRecordDto ToDto(MunicipalFinanceRecord r) => new(
        r.BfsNumber.Value, r.MunicipalityName, r.Year,
        r.SelfFinancingRatio, r.SelfFinancingShare, r.InterestBurdenShare, r.CapitalServiceShare,
        r.InvestmentShare, r.GrossDebtShare, r.NetDebtPerCapitaChf, r.NetDebtQuotient, r.BalanceSheetSurplusQuotient);
}
```

NOTE: this changes `GetPageAsync`'s signature from `(int page, int pageSize, …)` to `(FinanceListRequest, …)`. The only caller is `src/Kantonal.Api/Program.cs` — Step 6 updates it so the build stays green.

- [ ] **Step 6: Keep the Api caller compiling**

In `src/Kantonal.Api/Program.cs`, the `GET /api/finance` handler currently calls `service.GetPageAsync(page ?? 1, pageSize ?? 20, ct)`. Update that one call so it builds a `FinanceListRequest` (filters/sort wired fully in Task 6):

```csharp
app.MapGet("/api/finance", async (FinanceQueryService service, int? page, int? pageSize, CancellationToken ct) =>
{
    var request = new FinanceListRequest(null, null, null, null, page ?? 1, pageSize ?? 20);
    var result = await service.GetPageAsync(request, ct);
    return Results.Ok(ApiEnvelope.Success(result));
});
```

- [ ] **Step 7: Run the full suite**

Run: `dotnet test`
Expected: PASS — the four new service tests plus everything else (the contract test still returns the seeded rows through the default request).

- [ ] **Step 8: Commit**

```bash
git add src/Kantonal.Application/Errors/ src/Kantonal.Application/FinanceListRequest.cs src/Kantonal.Application/FinanceQueryService.cs src/Kantonal.Api/Program.cs tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs
git commit -m "feat(application): finance list request, sort validation, single-record lookup with typed errors"
```

---

### Task 6: API — global exception handler + filter/sort params + single-record endpoint

Map typed errors to the envelope via `IExceptionHandler`, wire the full query params on the list endpoint, and add `GET /api/finance/{bfs}/{year}`.

**Files:**
- Create: `src/Kantonal.Api/GlobalExceptionHandler.cs`
- Modify: `src/Kantonal.Api/Program.cs`
- Test: `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`

**Interfaces:**
- Consumes: `FinanceQueryService.GetPageAsync(FinanceListRequest, …)`, `GetByKeyAsync(int, int, …)`, `NotFoundException`, `ValidationException`, `ApiEnvelope`.
- Produces: `GET /api/finance/{bfs:int}/{year:int}`; the list endpoint reads `municipality`, `year`, `sortBy`, `sortDir`; errors return `{ ok:false, error:{ code, message } }` with 400/404/500.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs` (reuse the existing `Envelope`/`Data`/`Item` records, `TestApi`, and the `FakeFinanceImportSource` — note its three rows: Aadorf, Affeltrangen, Amlikon-Bissegg):

```csharp
    [Fact]
    public async Task GetFinance_FiltersByMunicipality()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance?municipality=aadorf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Envelope>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Data!.Total);
        Assert.Equal("Aadorf", Assert.Single(body.Data.Items).MunicipalityName);
    }

    [Fact]
    public async Task GetFinanceByKey_ReturnsRecord()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance/4551/2024");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SingleEnvelope>();
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.Equal("Aadorf", body.Data!.MunicipalityName);
    }

    [Fact]
    public async Task GetFinanceByKey_Returns404Envelope_WhenAbsent()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance/9999/2024");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.NotNull(body);
        Assert.False(body!.Ok);
        Assert.Equal("finance_record_not_found", body.Error!.Code);
    }

    [Fact]
    public async Task GetFinance_Returns400Envelope_OnBadSortField()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance?sortBy=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.NotNull(body);
        Assert.False(body!.Ok);
        Assert.Equal("invalid_sort_field", body.Error!.Code);
    }

    // Response shapes for the new endpoints/errors:
    public record SingleEnvelope(bool Ok, Item? Data);
    public record ErrorEnvelope(bool Ok, ApiError? Error);
    public record ApiError(string Code, string Message);
```

(The existing `Item` record already has `BfsNumber, MunicipalityName, Year, SelfFinancingRatio, NetDebtPerCapitaChf`; the extra ratios deserialize as absent/null and are not asserted here.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Kantonal.Tests --filter "GetFinance_FiltersByMunicipality|GetFinanceByKey_ReturnsRecord|GetFinanceByKey_Returns404Envelope_WhenAbsent|GetFinance_Returns400Envelope_OnBadSortField"`
Expected: FAIL — no single-record endpoint; no `municipality`/`sortBy` handling; bad sort currently throws an unhandled 500, and the not-found path 404s without an envelope (no handler yet).

- [ ] **Step 3: Create the exception handler**

Create `src/Kantonal.Api/GlobalExceptionHandler.cs`:

```csharp
using Kantonal.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace Kantonal.Api;

/// <summary>Maps typed domain errors to HTTP status codes + the standard error envelope.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, code, message) = exception switch
        {
            NotFoundException nf => (StatusCodes.Status404NotFound, nf.Code, nf.Message),
            ValidationException ve => (StatusCodes.Status400BadRequest, ve.Code, ve.Message),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(ApiEnvelope.Error(code, message), ct);
        return true;
    }
}
```

- [ ] **Step 4: Wire it + the endpoints in `Program.cs`**

In `src/Kantonal.Api/Program.cs`:

1. After `builder.Services.AddSwaggerGen();`, register the handler:

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
```

2. Immediately after `var app = builder.Build();`, enable it (before the endpoint mappings):

```csharp
app.UseExceptionHandler();
```

3. Replace the `GET /api/finance` handler to read the full query params, and add the single-record endpoint next to it:

```csharp
app.MapGet("/api/finance", async (FinanceQueryService service,
    string? municipality, int? year, string? sortBy, string? sortDir, int? page, int? pageSize,
    CancellationToken ct) =>
{
    var request = new FinanceListRequest(municipality, year, sortBy, sortDir, page ?? 1, pageSize ?? 20);
    var result = await service.GetPageAsync(request, ct);
    return Results.Ok(ApiEnvelope.Success(result));
});

app.MapGet("/api/finance/{bfs:int}/{year:int}", async (FinanceQueryService service, int bfs, int year, CancellationToken ct) =>
{
    var dto = await service.GetByKeyAsync(bfs, year, ct);
    return Results.Ok(ApiEnvelope.Success(dto));
});
```

Ensure `using Kantonal.Application;` is present (it is). `ApiEnvelope` and `GlobalExceptionHandler` are in `Kantonal.Api` (the `Program` assembly), no extra using needed.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS — the four new API tests plus all prior tests. The `POST /api/import` test and `GET /api/finance` base test still pass.

- [ ] **Step 6: Commit**

```bash
git add src/Kantonal.Api/GlobalExceptionHandler.cs src/Kantonal.Api/Program.cs tests/Kantonal.Tests/Api/FinanceEndpointTests.cs
git commit -m "feat(api): typed-error envelope handler, filter/sort params, single-record endpoint"
```

---

### Task 7: Web — render all 9 ratios

Extend the Blazor row model + client test and add the new table columns.

**Files:**
- Modify: `src/Kantonal.Web/Models/FinanceModels.cs`
- Modify: `src/Kantonal.Web/Components/Pages/Finance.razor`
- Test: `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs`

**Interfaces:**
- Consumes: the API JSON now carries 9 camelCase ratio fields.
- Produces: `FinanceRow` with all 9 ratios; the table renders them.

- [ ] **Step 1: Write the failing test**

In `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs`, extend the test JSON with the new fields and assert one of them deserializes:

```csharp
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
        Assert.Equal(128.37m, page.Items[0].BalanceSheetSurplusQuotient);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Kantonal.Tests --filter GetAsync_UnwrapsEnvelopeIntoRows`
Expected: FAIL — `FinanceRow` has no `BalanceSheetSurplusQuotient`.

- [ ] **Step 3: Extend `FinanceRow`**

Replace `src/Kantonal.Web/Models/FinanceModels.cs`:

```csharp
namespace Kantonal.Web.Models;

public record FinanceRow(
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

public record FinancePage(IReadOnlyList<FinanceRow> Items, int Page, int PageSize, int Total);
```

- [ ] **Step 4: Add the table columns**

In `src/Kantonal.Web/Components/Pages/Finance.razor`, replace the `<thead>` row and the `<tr>` inside the `@foreach` so all 9 ratios render. New `<thead>`:

```razor
            <tr>
                <th>BFS</th><th>Municipality</th><th>Year</th>
                <th>Self-financing ratio</th><th>Self-financing share</th><th>Interest burden share</th>
                <th>Capital service share</th><th>Investment share</th><th>Gross debt share</th>
                <th>Net debt/capita CHF</th><th>Net debt quotient</th><th>Balance-sheet surplus quotient</th>
            </tr>
```

New body row:

```razor
                <tr>
                    <td>@row.BfsNumber</td>
                    <td>@row.MunicipalityName</td>
                    <td>@row.Year</td>
                    <td>@row.SelfFinancingRatio</td>
                    <td>@row.SelfFinancingShare</td>
                    <td>@row.InterestBurdenShare</td>
                    <td>@row.CapitalServiceShare</td>
                    <td>@row.InvestmentShare</td>
                    <td>@row.GrossDebtShare</td>
                    <td>@row.NetDebtPerCapitaChf</td>
                    <td>@row.NetDebtQuotient</td>
                    <td>@row.BalanceSheetSurplusQuotient</td>
                </tr>
```

- [ ] **Step 5: Run the suite + build the web project**

Run: `dotnet test tests/Kantonal.Tests --filter GetAsync_UnwrapsEnvelopeIntoRows` then `dotnet build src/Kantonal.Web`
Expected: test PASS; web build succeeds (Razor compiles).

- [ ] **Step 6: Commit**

```bash
git add src/Kantonal.Web/Models/FinanceModels.cs src/Kantonal.Web/Components/Pages/Finance.razor tests/Kantonal.Tests/Web/FinanceApiClientTests.cs
git commit -m "feat(web): render all nine finance ratios in the dashboard table"
```

---

### Task 8: README + final verification

Document the completed API surface and verify the whole branch.

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the README**

In `README.md`, under the existing `## Data` section (after the import paragraph), add an API reference subsection:

```markdown
### API

- `GET /api/finance` — paged list. Query params: `municipality` (case-insensitive
  substring), `year` (exact), `sortBy` (one of `municipalityName`, `year`, or any of the
  nine ratio names e.g. `selfFinancingRatio`), `sortDir` (`asc`/`desc`), `page`, `pageSize`
  (1–100). Ratio sorts place missing values last. Envelope: `{ ok, data:{ items, page, pageSize, total } }`.
- `GET /api/finance/{bfs}/{year}` — a single municipality-year record, or `404` with
  `{ ok:false, error:{ code, message } }`.
- `POST /api/import` — re-run the importer (idempotent). Unauthenticated, dev-only (see caveat above).

Errors use the envelope `{ ok:false, error:{ code, message } }`: `400` for invalid
query input, `404` for an unknown record, `500` (generic, details logged server-side) otherwise.

The nine indicators are the HRM2 key figures from dataset `sk-stat-4`; each maps to a
documented source field on `FinanceIndicators`.
```

- [ ] **Step 2: Full verification**

Run, in order:

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Expected: build clean (0 warnings); all tests green; format reports no changes (run `dotnet format` and re-stage if it does).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: document the full finance API surface"
```

---

## Final verification (after all tasks)

- [ ] `dotnet test` — all green.
- [ ] `dotnet build` — no new warnings.
- [ ] Optional manual smoke (Docker + network): `docker compose up`, then
  `curl -s "localhost:8080/api/finance?sortBy=selfFinancingRatio&sortDir=desc&pageSize=5"` returns
  the highest self-financing-ratio municipalities; `curl -s -i localhost:8080/api/finance/9999/2024`
  returns `404` with the error envelope.
- [ ] Open a PR to `main` (merge commit, not squash) mirroring #1/#2; body has summary + test plan;
  note that this closes import-job review finding (B) (import-endpoint failures now return the error envelope).
- [ ] Update `HANDOFF.md` / memory: follow-up #2 done; follow-up #3 (dashboard controls + chart) next.

## Self-review notes (author)

- **Spec coverage:** 9 ratios on the entity (Task 1) ✓ + persisted/migrated (Task 2) ✓ + imported (Task 3) ✓;
  filter + sort in SQL with nulls-last (Task 4) ✓; single-record endpoint (Tasks 4–6) ✓; typed errors → HTTP
  envelope (Tasks 5–6) ✓; conservative sourced names (Task 1 + Global Constraints) ✓; Web renders them (Task 7) ✓;
  README (Task 8) ✓.
- **Type consistency:** `FinanceIndicators` 9-arg order, `FinanceSortField` 11 members, `FinanceQuery`,
  `FinanceListRequest`, and the `IFinanceRepository` signatures are used identically across Tasks 1/4/5/6.
  `Kantonal.Application.Errors.{NotFoundException,ValidationException}` referenced consistently in Tasks 5/6.
- **EF reality:** the dual-ctor + flat-columns + `Ignore(Indicators)` approach is spike-verified on InMemory;
  case-insensitive filter uses `ToLower().Contains` (not `ILike`) for InMemory portability; nulls-last is an
  explicit `OrderBy(r => r.X == null)` key, provider-independent.
- **Assumption that could be wrong:** the seven new German source field names match the live payload exactly
  (captured 2026-06-26 from the import-job work). If the portal renames a field, the single point to update is
  `ThurgauFinanceImporter.FinanceFields` (Task 3) and the importer mapping test.
