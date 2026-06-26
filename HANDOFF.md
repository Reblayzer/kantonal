# Kantonal — Session Handoff

**Written:** 2026-06-26 · **Branch:** `feature/dashboard` (10 commits ahead of `main` `601a225`) · **Repo:** https://github.com/Reblayzer/kantonal

## Goal & status

Kantonal is a clean-architecture ASP.NET Core (.NET 8) service exposing Swiss cantonal/municipal
finance data (opendata.swiss, Canton Thurgau dataset `sk-stat-4`) via a REST API + Blazor dashboard.
CV/portfolio project — see `PROJECT_BRAINSTORM.md` (integrity guardrail: no invented metrics/claims).

**Done & merged to `main` (prior sessions):**
- Walking skeleton — PR #1. Live import job — PR #2. Full API + domain (9 ratios, filter/sort, typed errors) — PR #3 (`601a225`).

**This session — Follow-up #3 (dashboard) — IN PROGRESS on `feature/dashboard`, NOT yet a PR:**
Built via subagent-driven development (TDD, per-task review). Spec + plan committed, then 10 tasks:

| # | Commit | What | Review |
|---|--------|------|--------|
| — | `190f249` | design spec | — |
| — | `40cbeee` | implementation plan | — |
| 1 | `3f1d535` | infra: repo `GetFilterOptionsAsync` (distinct municipalities/years) | clean |
| 2 | `d05e63b` | api: `GET /api/finance/options` + service delegate | clean |
| 3 | `914aa44` | web client: `GetAsync(FinanceQuery)` + `GetOptionsAsync` | clean |
| 4 | `d514626` | web: `RatioFormat` (percent/CHF/null, Swiss `'`) | clean |
| 5 | `b6a703a` | web: `RatioCatalog` (9 ratios, single source of truth) | clean |
| 6 | `9e104b8` | web: `SortState.Toggle` | clean |
| 7 | `00f1c8e` | web: `BarChartGeometry` (pure SVG layout) | clean |
| 8 | `99a716c` | web: `FinanceTable.razor` (sortable headers) | clean |
| 9 | `08ce5ad` | web: `RatioBarChart.razor` (inline SVG) | clean |
| 10 | `280e6ea` | web: wire up `Finance.razor` + `app.css` | **PENDING** |

**Tests: 62/62 green**, build 0 warnings, `dotnet format` clean (as of `280e6ea`).

## ⚠️ What is NOT done yet (resume here)

1. **Task 10 task-review** — was dispatched but interrupted by the handoff. **Re-run it first.** It's the
   integration glue: verify the page wires correctly to Tasks 1–9 (members/signatures, chart data flow,
   filter-resets-to-page-1, paging keeps filters, `@bind:after`/`@onclick` handlers, null-safety on
   `_options`/`_page`). The build is clean (so signatures match), but check the data flow semantically.
2. **Final whole-branch review** — not done. Per SDD, dispatch the broad code-reviewer (opus) over
   `git merge-base main HEAD`..HEAD before finishing. Feed it the deferred Minors list (below).
3. **Manual in-browser verification** — deferred all session (no local Postgres). The page logic is
   covered by the automated suite, but the dashboard has never been seen rendering. Worth a real run
   (`dotnet run --project src/Kantonal.Api` + `src/Kantonal.Web`, open `/finance`) — see plan Task 10 Step 5.
4. **Branch not yet pushed when this was written** — being pushed as part of this handoff; verify on resume
   with `git status` / `git log origin/feature/dashboard`.
5. **No PR opened yet.** After reviews pass + manual check, open PR like #1/#2/#3, then merge.

## Deferred Minors (carry into the final review — none are blockers)

- **T5:** `RatioCatalogTests` selector test spot-checks only 3 of 9 entries; no assertion that exactly one
  entry is `RatioUnit.Chf`. (A 1-line count assertion + full selector coverage would harden the catalog.)
- **T7:** `BarChartGeometry.MaxValue` not normalized for all-negative input (returns the negative max).
  Harmless today — our chart never consumes `MaxValue` for an axis, and feeds top-N-descending data. Add
  `Math.Max(0m, max)` only if a value axis is ever added. Also no `gap>0` positional test.
- **T4:** `RatioFormat.Swiss` `NumberFormatInfo` is mutable (only `private`, so safe); `NumberGroupSizes`
  assignment is redundant. Cosmetic.
- **T2:** new test response records sit above the `// Response shapes` comment instead of under it. Cosmetic.

## Key decisions / facts (so you don't re-derive them)

- **Percent fields are already percentages** in the source (e.g. `163.81`) → rendered `"163.8 %"`. Only
  `NetDebtPerCapitaChf` is CHF (`"1'234 CHF"`). `null` → `"—"`. (`RatioFormat`.)
- **No new NuGet packages, no bUnit.** Razor components are thin renderers over pure, unit-tested helpers
  (`RatioFormat`, `RatioCatalog`, `SortState`, `BarChartGeometry`); the `.razor`/CSS files have no automated
  tests by design — verified by clean build + the helper tests.
- **Chart reuses the list endpoint** — no new query: `GetAsync(Year, SortBy=ratioKey, SortDir=Desc, PageSize=15)`,
  then map via the ratio's `Selector`, drop nulls. Defaults: latest year + Self-financing ratio.
- **Sort keys sent to the API must match `FinanceSortField` enum names** — `RatioCatalog` keys + `SortState`
  field strings are built to satisfy this. `SortState.Asc/Desc` = `"Asc"/"Desc"` (match `SortDirection`).
- **Justified deviation (T9):** Razor reserves `<text>` as a directive (RZ1023), so SVG labels render via a
  `BarLabel()` helper returning `MarkupString` with `WebUtility.HtmlEncode` + invariant-culture coords
  (reviewer confirmed safe). `@using System.Net` added for it.

## Pointers to read on resume

- **This `HANDOFF.md` + `git log` are the portable record** — the per-task commits/statuses/decisions above
  are the source of truth on a fresh machine. (The SDD ledger and briefs live under `.superpowers/sdd/`,
  which is **git-ignored scratch — it will NOT be present after a fresh clone on another PC.** Don't go
  looking for it there; everything you need to resume is in this doc.)
- `docs/superpowers/plans/2026-06-26-kantonal-dashboard.md` — the 10-task plan (Task 10 Step 5 = manual run).
- `docs/superpowers/specs/2026-06-26-kantonal-dashboard-design.md` — the design spec.
- `~/dev/CLAUDE.md` — workflow rules.

## Recommended resume sequence

1. `git status` + `git log origin/feature/dashboard` — confirm the branch is pushed and you're on it.
2. Re-read this `HANDOFF.md` (the SDD scratch ledger won't be on a fresh clone).
3. Re-run the **Task 10 task-review** (SDD task-reviewer prompt; package = `review-package 08ce5ad 280e6ea`).
   Fix any Critical/Important, then re-review.
4. Run the **final whole-branch review** (opus; package = `review-package $(git merge-base main HEAD) HEAD`),
   feeding it the deferred-Minors list.
5. Do the **manual in-browser run** of `/finance` (needs Postgres).
6. `superpowers:finishing-a-development-branch` → open the PR.
