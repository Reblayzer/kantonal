# Kantonal — Full API + Domain (Follow-up #2) Design

**Date:** 2026-06-26 · **Status:** approved · **Branch:** `feature/full-api`

## Goal

Complete the Kantonal REST API: model all **9 HRM2 financial indicators**, add
filtering + sorting + a single-record endpoint, and introduce typed errors mapped
to HTTP via the existing response envelope. One plan, one PR (like #1/#2).

Builds on follow-up #1 (live importer, merged as `0ff8501`). The importer already
fetches all 9 ratios from opendata.swiss; today only 2 are persisted.

## Integrity guardrail

`PROJECT_BRAINSTORM.md` forbids invented metrics/claims. The 7 new ratios are
standard Swiss HRM2 municipal indicators. We use **conservative English names with
a consistent translation rule** and document each property with its **exact German
source field name** — no invented prose about what each ratio "means" financially.

## 1. Domain — `FinanceIndicators` value object

The entity ctor today takes 5 args. Adding 7 ratios → 12 args, violating the
"≤4 params, use a config object" rule (`~/dev/CLAUDE.md`). So the 9 ratios are
grouped into a `FinanceIndicators` record (a value object — value equality, no
identity), and the entity becomes:

```csharp
MunicipalFinanceRecord(BfsNumber bfsNumber, string municipalityName, int year, FinanceIndicators indicators)
```

`FinanceIndicators` holds 9 `decimal?` properties. Translation rule: *grad* → `Ratio`,
*anteil* → `Share`, *quotient* → `Quotient`. Each property carries an XML doc comment
citing its German source field.

| German source field (opendata.swiss) | C# property |
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

All 9 are nullable (`decimal?`) — the source may omit any of them. The existing
`SelfFinancingRatio` and `NetDebtPerCapitaChf` move into the value object; no ratio
meaning is invented.

## 2. Persistence + importer

- **EF mapping:** `FinanceIndicators` is mapped as an EF Core 8 **complex type**
  (`ComplexProperty`) so all 9 columns live on the existing `finance_records` table —
  no join, no separate identity. Column names: snake_case of each property
  (`self_financing_ratio`, `self_financing_share`, …), `numeric` type, all nullable.
- **Migration:** a new EF migration adds the 7 new columns to `finance_records`. The
  2 existing columns (`self_financing_ratio`, `net_debt_per_capita_chf`) keep their
  current names so existing rows are preserved.
- **Importer:** `ThurgauFinanceImporter.FinanceFields` gains the 7 new JSON fields;
  `Map` builds a `FinanceIndicators` from them with the same tolerant `double?→decimal?`
  parsing already in place. Bad-row skipping (unparseable bfs/year, blank name) is
  unchanged.

## 3. Application + API surface

### Query spec
The repository read method changes from `GetAsync(skip, take, ct)` to accept a small
spec object so filtering/sorting/paging push into SQL (never in-memory — perf rule):

```csharp
public sealed record FinanceQuery(
    string? Municipality,   // case-insensitive substring; null = no filter
    int? Year,              // exact; null = no filter
    FinanceSortField SortBy,
    SortDirection Direction,
    int Skip,
    int Take);

public enum FinanceSortField { MunicipalityName, Year,
    SelfFinancingRatio, SelfFinancingShare, InterestBurdenShare, CapitalServiceShare,
    InvestmentShare, GrossDebtShare, NetDebtPerCapitaChf, NetDebtQuotient, BalanceSheetSurplusQuotient }

public enum SortDirection { Asc, Desc }
```

`IFinanceRepository` becomes:
```csharp
Task<IReadOnlyList<MunicipalFinanceRecord>> QueryAsync(FinanceQuery query, CancellationToken ct);
Task<int> CountAsync(string? municipality, int? year, CancellationToken ct);   // count honours the same filters
Task<MunicipalFinanceRecord?> GetByKeyAsync(BfsNumber bfsNumber, int year, CancellationToken ct);
Task<int> UpsertManyAsync(IReadOnlyList<MunicipalFinanceRecord> records, CancellationToken ct);  // unchanged
```

- Filtering: `Municipality` → `WHERE municipality_name ILIKE '%x%'` (EF `Contains`,
  case-insensitive); `Year` → `WHERE year = x`. Both optional, combined with AND.
- Sorting: `ORDER BY <sortBy> <dir>`, then a stable tiebreak (`municipality_name, year`).
  Ratio columns sort **nulls-last** regardless of direction (so a "top by ratio" query
  doesn't surface empty rows first).
- Paging unchanged: `Skip`/`Take`, `pageSize` clamped 1..100, `page` floored to 1.

### Endpoints
- `GET /api/finance?municipality=&year=&sortBy=&sortDir=&page=&pageSize=` →
  `{ ok, data:{ items, page, pageSize, total } }` where `total` honours the filters.
  Unknown `sortBy`/`sortDir` → `ValidationException` (400).
- `GET /api/finance/{bfs:int}/{year:int}` → `{ ok, data: <record> }`, or 404 if absent.
- `FinanceRecordDto` grows to all 9 ratios (flat properties, camelCase JSON).

`FinanceQueryService` maps API-level strings (`sortBy`, `sortDir`) to the enums,
throwing `ValidationException` on an unknown value, and builds the `FinanceQuery`.

## 4. Error handling — typed errors → HTTP

- Typed exceptions in a shared area (`Kantonal.Application` `Shared`/errors namespace):
  `NotFoundException(string code, string message)` and
  `ValidationException(string code, string message)`.
- A global `IExceptionHandler` (registered via `AddExceptionHandler` +
  `UseExceptionHandler`) maps:
  - `NotFoundException` → 404
  - `ValidationException` → 400
  - anything else → 500 with a generic message; the real detail is logged server-side
    only (no internal details leaked to clients).
  Body in every case is `ApiEnvelope.Error(code, message)`.
- The service layer throws typed errors; the handler does the HTTP mapping (per
  `~/dev/CLAUDE.md`: "Map errors to HTTP codes in the handler, not the service").
- Side benefit: this fixes import-job review finding (B) — `POST /api/import` failures
  now return the `{ ok:false, error }` envelope instead of a raw 500.

## 5. Web (Blazor)

The Blazor finance table's model (`FinanceRow`) and the typed `FinanceApiClient`
extend to carry all 9 ratios; the table renders the new columns (read-only).
**Filter/sort UI controls remain follow-up #3** — but the client + model already
carry the fields, so #3 only adds controls + a chart.

## 6. Out of scope (later follow-ups)

- Filter/sort **UI controls** and a chart → follow-up #3.
- Auth + rate-limiting on `POST /api/import` → tracked hardening (before any deploy).
- CI/CD + Azure notes → follow-up #4.

## 7. Testing strategy

- **Unit (Domain):** `FinanceIndicators` equality; entity construction with indicators.
- **Unit (Infrastructure):** importer maps all 9 fields from a captured payload;
  tolerates nulls; repository `QueryAsync` filter (municipality substring + year),
  sort by name/year/a ratio with **nulls-last**, and `CountAsync` honouring filters,
  and `GetByKeyAsync` found/not-found — against the InMemory provider.
- **Unit (Application):** `FinanceQueryService` maps sort strings → enums, throws
  `ValidationException` on unknown `sortBy`/`sortDir`, builds the right `FinanceQuery`.
- **Integration (Api, offline via `WebApplicationFactory` + fake import source):**
  filtered/sorted list response; single-record 200; single-record 404 returns the
  error envelope; bad `sortBy` returns 400 envelope.

All tests deterministic and offline (no live network), consistent with follow-up #1.

## 8. Risks / assumptions

- **EF complex types + Npgsql + InMemory:** `ComplexProperty` must work under both
  the Npgsql provider (real columns) and the InMemory provider (tests). If a provider
  mismatch surfaces, the fallback is mapping the 9 ratios as flat owned/plain
  properties on the entity — same columns, slightly more ceremony. The plan's first
  domain/persistence task verifies this against InMemory before building on it.
- **Nulls-last ordering in EF→SQL:** expressed via a `.OrderBy(r => r.X == null)` key
  before the real sort key, which EF translates to SQL; verified by a repository test.
- Translation names are conservative and sourced; if a reviewer prefers a different
  English term, it is a rename (cheap), not a behavior change.
