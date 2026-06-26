# Tailored project — Kantonal

**For:** ELCA Informatique SA (ELCA Group), Junior .NET Engineer, Zürich
**Stack the posting wants this project to prove:** .NET / C#, ASP.NET Core, clean architecture, tests, Docker, CI/CD, Cloud (Azure), Git, AI-assisted development (Claude Code / GitHub Copilot)

## One-line pitch

A small ASP.NET Core service that ingests an open Swiss public-administration dataset (opendata.swiss) and exposes it through a clean REST API plus a Blazor dashboard. It mirrors what ELCA actually does: production .NET for Swiss public administration.

## Why this project for this job

ELCA builds tailored software for large Swiss public administrations and private companies. A civic open-data service in ASP.NET Core, built with clean architecture and a full DevOps chain, is the closest honest analog to their day-to-day work, and it carries every core technology the posting asks for. It is also distinct from the Kotlin "Perron" project (Ergon application), so it adds .NET breadth rather than repeating an idea.

## Posting technologies it demonstrates

- **.NET / C#** — the whole backend (ASP.NET Core Web API).
- **Saubere Architektur (clean architecture)** — Domain / Application / Infrastructure / Api separation.
- **Tests** — xUnit unit tests on the domain and application layers; a couple of integration tests on the API.
- **Docker** — the API, database and frontend run from one `docker compose up`.
- **CI/CD** — GitHub Actions pipeline (build, test, lint, `dotnet format` check).
- **Cloud (Azure)** — written so it deploys to Azure App Service + Azure Database for PostgreSQL; documented in the README.
- **Git** — public repo, clean commit history.
- **AI-assisted development** — built with Claude Code (custom skills + MCP) and reviewed critically before merging; noted in the README.

## Architecture sketch

```
opendata.swiss CSV/JSON  ->  Infrastructure (typed HttpClient + import job)
                                   |
                              Domain (entities, value objects, rules)
                                   |
                              Application (services, DTOs, validation)
                                   |
                              Api (ASP.NET Core REST endpoints, EF Core)  ->  PostgreSQL
                                   |
                              Blazor dashboard (filter, sort, charts)
```

- **Domain:** the civic dataset modelled as proper entities (e.g. municipal finance rows, or population statistics), with value objects and validation. No framework dependencies.
- **Application:** query/import services, DTOs, mapping, input validation.
- **Infrastructure:** EF Core (PostgreSQL), migrations, a typed `HttpClient` that pulls the dataset from opendata.swiss on a scheduled import.
- **Api:** ASP.NET Core controllers/minimal API, filtering + pagination, OpenAPI/Swagger.
- **Frontend:** Blazor dashboard (filter by canton/municipality/year, sort, a simple chart).

## Dataset choice

Pick one finance or population dataset from opendata.swiss (for example cantonal/municipal finances or resident statistics). All of it is public, non-personal data, so there is no PII or GDPR surface to worry about. Confirm the exact dataset at build time and link it in the README.

## v1 scope

**In:**
- Clean-architecture solution: `Kantonal.Domain`, `Kantonal.Application`, `Kantonal.Infrastructure`, `Kantonal.Api`, plus `Kantonal.Web` (Blazor) and `Kantonal.Tests`.
- Import job that fetches the dataset and stores it in PostgreSQL via EF Core.
- REST API: list + filter + paginate + single-record endpoints, with Swagger.
- Blazor dashboard reading the API: table with filter/sort and one chart.
- xUnit tests on domain + application logic, one or two API integration tests.
- `docker compose` for api + db + web.
- GitHub Actions CI: restore, build, test, `dotnet format --verify-no-changes`.
- README: architecture, how to run, the Azure deployment notes, the AI-assisted workflow.

**Out (v1):**
- Authentication / RBAC (the data is public read-only; keep it simple).
- Real Azure deployment (document it; only deploy if it stays cheap/free).
- Multiple datasets or a heavy ETL pipeline.
- Fancy charting library beyond one simple chart.

## Build plan (a few days to ~2 weeks, solo)

1. Scaffold the clean-architecture solution and the Docker/compose setup.
2. Model the chosen dataset in Domain; add EF Core + migrations in Infrastructure.
3. Build the import job (typed HttpClient against opendata.swiss).
4. Build the REST API (filter/paginate, Swagger) on top of Application services.
5. Add xUnit tests for domain/application; one or two API integration tests.
6. Build the Blazor dashboard (table + filter/sort + one chart).
7. Wire GitHub Actions CI; write the README incl. Azure deployment notes.
8. Make the repo public: github.com/Reblayzer/kantonal

## Integrity guardrails

- Describe behaviour and stack only. No invented metrics, users, performance numbers, or "deployed in production" claims.
- Add the public repo link to the CV only once the repo exists (same day Alex starts building).
- Everything here is something Alex can build and fully explain in an interview.
