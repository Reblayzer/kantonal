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

The import is an idempotent upsert keyed on `(BfsNumber, Year)`, so it is safe to
re-run. Only two KPI ratios are modelled today (self-financing ratio, net debt per
capita); the remaining ratios are a planned follow-up.

## Architecture

`Domain` ← `Application` ← `Infrastructure` (EF Core/Postgres) + `Api` (composition
root). `Web` (Blazor) is a separate client calling the API over HTTP.

Built with AI-assisted development (Claude Code) and reviewed before merging.
Azure deployment notes are tracked in follow-up work.
