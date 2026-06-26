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

## Architecture

`Domain` ← `Application` ← `Infrastructure` (EF Core/Postgres) + `Api` (composition
root). `Web` (Blazor) is a separate client calling the API over HTTP.

Built with AI-assisted development (Claude Code) and reviewed before merging.
Azure deployment notes and the opendata.swiss import job are tracked in follow-up work.
