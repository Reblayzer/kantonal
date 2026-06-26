# Kantonal

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

Built with AI-assisted development (Claude Code) and reviewed before merging.
Azure deployment notes are tracked in follow-up work.
