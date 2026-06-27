# Kantonal

[![CI](https://github.com/Reblayzer/kantonal/actions/workflows/ci.yml/badge.svg)](https://github.com/Reblayzer/kantonal/actions/workflows/ci.yml)

ASP.NET Core (clean architecture) service exposing Swiss cantonal/municipal
finance data (opendata.swiss, Canton Thurgau dataset `sk-stat-4`) via a REST API
and a Blazor dashboard.

## Run with Docker

```bash
docker compose up --build
```

- API + Swagger: http://localhost:8080/swagger
- Blazor dashboard: http://localhost:5080/finance
- PostgreSQL: localhost:5432 (kantonal / postgres / postgres)

> **Local development only.** The credentials above are defaults for local development and must never be used for any deployment. Use environment variables or a secrets manager to supply credentials in staging and production environments.

## Run tests

```bash
dotnet test Kantonal.sln
```

## Data

Finance data is imported from the Kanton Thurgau open-data portal
(opendata.swiss dataset `sk-stat-4`, served via the Opendatasoft records API at
`https://data.tg.ch/`). The API imports all records at startup (failure-tolerant:
an outage logs an error and the app still starts) and on demand via:

    POST /api/import   ->   { "ok": true, "data": { "imported": <count> } }

> **Unauthenticated, local development only.** `POST /api/import` has no auth or
> rate limiting yet — it must be gated before the service is exposed in any shared
> environment. Authorization is tracked as follow-up work.

The import is an idempotent upsert keyed on `(BfsNumber, Year)`, so it is safe to
re-run.

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

## Architecture

`Domain` ← `Application` ← `Infrastructure` (EF Core/Postgres) + `Api` (composition
root). `Web` (Blazor) is a separate client calling the API over HTTP.

## AI-assisted development

This project was built with Claude Code using a spec-driven workflow: each feature
started as a brainstormed design spec, was expanded into a task-by-task implementation
plan, then built test-first (TDD) — usually via subagent-driven development, one
reviewed task per commit. Every change went through a code-review pass over the diff
and was merged via pull request, with CI gating build, tests, and formatting. The
design specs and implementation plans are kept under `docs/superpowers/`.

## Azure deployment notes

> **Not currently deployed.** These notes describe how the project is structured to run
> on Azure — they are a deployment recipe, not a description of live infrastructure.

The two container images (`Kantonal.Api`, `Kantonal.Web`) plus a managed PostgreSQL map
onto Azure as:

- **Azure Database for PostgreSQL Flexible Server** — replaces the `db` Compose service;
  use its connection string.
- **Azure App Service for Containers** (or Container Apps) — one app for the API image
  and one for the Web image, built from the existing Dockerfiles. Both containers listen
  on port `8080`, so set `WEBSITES_PORT=8080` on each App Service.
- **Configuration** via app settings / environment variables — never commit secrets:
  - API: `ConnectionStrings__Kantonal` = the Flexible Server connection string.
  - Web: `ApiBaseUrl` = the API app's public URL.

On first boot the API applies its EF schema and runs the failure-tolerant import, so a
fresh database is populated automatically; gate `POST /api/import` behind auth before any
shared deployment (see the import caveat above). Intended to fit free/low-cost tiers —
deploy only if it stays cheap.
