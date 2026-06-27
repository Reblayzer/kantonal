# Kantonal — Follow-up #3: Dashboard controls + chart (design)

**Date:** 2026-06-26 · **Branch target:** off `main` (`601a225`) · **PR:** like #1/#2/#3

## Goal

Make the Blazor `/finance` dashboard interactive. The REST API already supports
filtering (municipality, year), sorting (any of the 9 HRM2 ratios, nulls-last),
and pagination; the Blazor page ignores all of it today. This work wires the
client through to those params, adds filter/sort controls, adds one chart, and
formats the ratio cells (currently raw `decimal?`).

CV/portfolio project — integrity guardrails from `PROJECT_BRAINSTORM.md` apply
(no invented metrics or claims). Dependency-light by preference.

## Scope (what changes)

1. A new read endpoint exposing distinct municipalities + years (for dropdowns).
2. The Web `FinanceApiClient` wired to the full query surface + the new endpoint.
3. Filter controls (municipality + year dropdowns), sortable table headers,
   numeric formatting, and one bar chart — all on `/finance`.

Out of scope: auth on `POST /api/import`, CI/CD (follow-up #4), any new ratios.

## Decisions (from brainstorming)

- **Chart:** bar chart — one selected ratio across municipalities for a selected
  year. Bounded to **top 15** by that ratio (descending).
- **Chart tech:** hand-rolled inline **SVG** (zero dependencies, server-rendered).
- **Filters:** **dropdowns populated from data** (distinct municipalities + years).
- **Sort UI:** **clickable column headers** (click to sort, click again to flip;
  ▲/▼ indicator).
- **Formatting:** the 8 ratio/share/quotient fields are already **percentages**
  in the source (German keys end `_in[_prozent]`) → render `"85.4 %"` (1 decimal).
  `NetDebtPerCapitaChf` is **CHF** → render `"1'234 CHF"` (Swiss `'` grouping).
  `null` → `"—"`.
- **Testing:** **no bUnit** (not installed). `.razor` files stay thin over pure,
  unit-tested helpers; everything else covered by existing test layers.

## Architecture

### 1. Backend — filter-options read path

Distinct values can't be derived client-side (480 rows > 100 max page size), so a
dedicated read path through all layers:

- **Domain/Application contract:**
  `IFinanceRepository.GetFilterOptionsAsync(CancellationToken)` →
  `FinanceFilterOptions(IReadOnlyList<string> Municipalities, IReadOnlyList<int> Years)`.
- **Infrastructure:** EF implementation — `Distinct()` on municipality name
  (ordered A→Z) and year (ordered newest→oldest). Two small queries; fine at this
  size.
- **Application:** `FinanceQueryService.GetFilterOptionsAsync` returns a DTO
  (`FinanceFilterOptionsDto(IReadOnlyList<string> Municipalities, IReadOnlyList<int> Years)`).
- **API:** `GET /api/finance/options` → `ApiEnvelope.Success({ municipalities, years })`.

### 2. Web client

- Generalize `FinanceApiClient.GetAsync` to accept a query object
  `FinanceQuery(string? Municipality, int? Year, string? SortBy, string? SortDir, int Page, int PageSize)`
  and build the query string skipping null/empty params (URL-encoded). Update the
  single existing call site in `Finance.razor`.
- Add `GetOptionsAsync(CancellationToken)` →
  `FilterOptions(IReadOnlyList<string> Municipalities, IReadOnlyList<int> Years)`.
- Keep the existing non-`ok` envelope → `InvalidOperationException` behavior.

### 3. Web UI — components over pure helpers

Pure helpers (in `Kantonal.Web`, no rendering, unit-tested):

- **`RatioCatalog`** — single source of truth for the 9 ratios. Each entry:
  `{ Key, Label, Unit, Selector }` where `Key` matches the API `sortBy` enum name,
  `Unit ∈ {Percent, Chf}`, `Selector: FinanceRow → decimal?`. Drives table
  columns, the chart's ratio picker, and per-cell formatting.
- **`RatioFormat`** — `Percent(decimal?)`, `Chf(decimal?)`, both `null → "—"`.
  Deterministic culture (fixed `NumberFormatInfo`: `'` group separator).
- **`BarChartGeometry`** — `(values, width, height) → { bars: [{x,y,width,height}], maxValue }`.
  Handles empty input and all-null/all-zero (no divide-by-zero).
- **`SortToggle`** — `(currentField, currentDir, clickedField) → (field, dir)`.
  Same field flips direction; a new field selects it ascending.

Components (thin renderers):

- **`FinanceTable.razor`** — params: `Items`, `SortBy`, `SortDir`,
  `EventCallback<string> OnSort`. Renders headers from `RatioCatalog` with ▲/▼ on
  the active column; formats cells via `RatioFormat` + the entry's `Unit`.
- **`RatioBarChart.razor`** — params: `Title`, `Unit`, and the
  `(label, value)` data. Emits an inline `<svg>` of `<rect>` bars from
  `BarChartGeometry`, with municipality labels and a value axis.
- **`Finance.razor`** — orchestrates state and composes the pieces:
  - Filter state: `municipality?`, `year?`, `sortBy`, `sortDir`, `page`.
  - Chart state: `chartYear` (default: latest available year), `chartRatio`
    (default: `SelfFinancingRatio`).
  - On init: `GetOptionsAsync` (populates both dropdowns + chart pickers), then
    the first table + chart load.
  - On any filter/sort/page change: reload the table via `GetAsync`.
  - On any chart-picker change: reload the chart via `GetAsync`.

### 4. Chart data flow (reuses the list endpoint)

No new query type. The chart calls
`GetAsync(year=chartYear, sortBy=chartRatio, sortDir=Desc, pageSize=15, page=1)`.
The API's nulls-last descending sort surfaces the 15 highest non-null values; the
page drops any remaining null-valued rows (via the ratio's `Selector`) before
handing `(municipalityName, value)` pairs to the chart.

### 5. Error / empty handling

- Options load once on init; a failure surfaces an inline error and leaves the
  page usable with empty dropdowns.
- List-load failure → inline error message instead of the table.
- Empty result set → "No records." and an empty-chart note.

## Testing strategy

| Unit | Test |
|---|---|
| `RatioCatalog` | each entry's `Selector` returns the matching property; `Key` matches the API enum name |
| `RatioFormat` | percent (1 dp + `%`), CHF (`'` grouping + ` CHF`), `null → "—"` |
| `BarChartGeometry` | scaling correct; empty input; all-null/all-zero (no div-by-zero) |
| `SortToggle` | same field flips dir; new field → Asc |
| `FinanceApiClient.GetAsync` | builds/encodes the query string from `FinanceQuery`; parses the page |
| `FinanceApiClient.GetOptionsAsync` | parses municipalities + years |
| `EfFinanceRepository.GetFilterOptionsAsync` | distinct + ordering (InMemory) |
| `FinanceQueryService.GetFilterOptionsAsync` | maps to DTO |
| `GET /api/finance/options` | envelope shape (`WebApplicationFactory`) |

**Deliberate tradeoff:** no component-render tests — bUnit isn't installed and
adding it conflicts with the dependency-light preference. The `.razor` files are
kept thin over the tested helpers; logic lives in the helpers, not the markup.

## One assumption that could be wrong

The source values for the 8 percentage fields are stored as **already-percent**
magnitudes (e.g. `85.4`), inferred from the `_in[_prozent]` German keys. If they
are actually fractions (`0.854`), `RatioFormat.Percent` must `× 100`. Verify
against real imported data during TDD before finalizing the formatter.
