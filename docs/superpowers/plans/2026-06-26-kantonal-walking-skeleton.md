# Kantonal Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a thin end-to-end slice of Kantonal — a clean-architecture ASP.NET Core solution where a Blazor page reads cantonal/municipal finance rows from PostgreSQL through a REST API, all running from one `docker compose up`.

**Architecture:** Clean architecture with strict dependency direction: `Domain` (no deps) ← `Application` (depends on Domain) ← `Infrastructure` (EF Core/Postgres, implements Application ports) and `Api` (composition root, wires everything); `Web` (Blazor) is a separate client that talks to `Api` over HTTP. This first plan proves the whole chain with a tiny seeded dataset; the opendata.swiss import job, full filtering/charts, and CI are follow-up plans.

**Tech Stack:** .NET 8 / C# 12, ASP.NET Core minimal APIs, EF Core 8 + Npgsql, PostgreSQL 16, Blazor Web App (Interactive Server), xUnit + Microsoft.AspNetCore.Mvc.Testing, Docker + docker compose.

## Global Constraints

- Target framework: `net8.0` for every project (SDK present: 8.0.422). Do not use `net9.0`+ APIs.
- Solution name `Kantonal`; project names exactly: `Kantonal.Domain`, `Kantonal.Application`, `Kantonal.Infrastructure`, `Kantonal.Api`, `Kantonal.Web`, `Kantonal.Tests`.
- Dependency rule: `Domain` references nothing; `Application` references only `Domain`; `Infrastructure` references `Application` (+ EF/Npgsql); `Api` references `Application` + `Infrastructure`; `Web` references nothing from the solution (HTTP client only). A task that violates this is wrong.
- API envelope: success `{ "ok": true, "data": ... }`, error `{ "ok": false, "error": { "code", "message" } }`. List endpoints return `data` as `{ items, page, pageSize, total }`.
- Dataset of record (used by the seeder and, later, the importer): opendata.swiss `gemeindefinanzkennzahlen-politische-gemeinden-kanton-thurgau`, source `https://data.tg.ch/api/v2/catalog/datasets/sk-stat-4/records`. Domain key is `(BfsNumber, Year)`.
- EF entities use `decimal` for financial ratios, `int` for `BfsNumber` and `Year`. Money/ratio values are nullable in source — model ratios as `decimal?`.
- TDD: every code task writes a failing test first. Stage specific files on commit; never `git add .`. Conventional commit messages.

---

### Task 0: Repository, solution, and project scaffold

**Files:**
- Create: `.gitignore`
- Create: `Kantonal.sln`
- Create: `src/Kantonal.Domain/Kantonal.Domain.csproj`
- Create: `src/Kantonal.Application/Kantonal.Application.csproj`
- Create: `src/Kantonal.Infrastructure/Kantonal.Infrastructure.csproj`
- Create: `src/Kantonal.Api/Kantonal.Api.csproj` (+ generated `Program.cs`, `appsettings*.json`)
- Create: `src/Kantonal.Web/Kantonal.Web.csproj` (+ generated Blazor template files)
- Create: `tests/Kantonal.Tests/Kantonal.Tests.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable solution with the dependency graph wired, so every later task can `dotnet build`.

- [ ] **Step 1: Initialize git and ignore build artifacts**

```bash
cd /home/reblayzer/dev/kantonal
git init
```

Create `.gitignore`:

```gitignore
bin/
obj/
*.user
.vs/
[Tt]est[Rr]esults/
*.db
.env
```

- [ ] **Step 2: Create the solution and class-library / app projects**

```bash
cd /home/reblayzer/dev/kantonal
dotnet new sln -n Kantonal
dotnet new classlib -n Kantonal.Domain -o src/Kantonal.Domain -f net8.0
dotnet new classlib -n Kantonal.Application -o src/Kantonal.Application -f net8.0
dotnet new classlib -n Kantonal.Infrastructure -o src/Kantonal.Infrastructure -f net8.0
dotnet new web -n Kantonal.Api -o src/Kantonal.Api -f net8.0
dotnet new blazor -n Kantonal.Web -o src/Kantonal.Web -f net8.0 --interactivity Server --empty
dotnet new xunit -n Kantonal.Tests -o tests/Kantonal.Tests -f net8.0
```

Delete the default `Class1.cs` files:

```bash
rm src/Kantonal.Domain/Class1.cs src/Kantonal.Application/Class1.cs src/Kantonal.Infrastructure/Class1.cs
```

- [ ] **Step 3: Add projects to the solution and wire references**

```bash
cd /home/reblayzer/dev/kantonal
dotnet sln add src/Kantonal.Domain src/Kantonal.Application src/Kantonal.Infrastructure src/Kantonal.Api src/Kantonal.Web tests/Kantonal.Tests
dotnet add src/Kantonal.Application reference src/Kantonal.Domain
dotnet add src/Kantonal.Infrastructure reference src/Kantonal.Application
dotnet add src/Kantonal.Api reference src/Kantonal.Application src/Kantonal.Infrastructure
dotnet add tests/Kantonal.Tests reference src/Kantonal.Domain src/Kantonal.Application src/Kantonal.Infrastructure src/Kantonal.Api
```

- [ ] **Step 4: Build the empty solution**

Run: `dotnet build Kantonal.sln`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add .gitignore Kantonal.sln src tests
git commit -m "chore(scaffold): clean-architecture solution skeleton"
```

---

### Task 1: Domain — MunicipalFinanceRecord entity + BfsNumber value object

**Files:**
- Create: `src/Kantonal.Domain/BfsNumber.cs`
- Create: `src/Kantonal.Domain/MunicipalFinanceRecord.cs`
- Test: `tests/Kantonal.Tests/Domain/MunicipalFinanceRecordTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `readonly record struct BfsNumber(int Value)` with `static BfsNumber Create(int value)` that throws `ArgumentOutOfRangeException` when `value <= 0`.
  - `sealed class MunicipalFinanceRecord` with ctor `(BfsNumber bfsNumber, string municipalityName, int year, decimal? selfFinancingRatio, decimal? netDebtPerCapitaChf)` and read-only properties of the same names; throws `ArgumentException` on null/blank `municipalityName` and `ArgumentOutOfRangeException` when `year < 1900`.

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Domain/MunicipalFinanceRecordTests.cs`:

```csharp
using Kantonal.Domain;

namespace Kantonal.Tests.Domain;

public class MunicipalFinanceRecordTests
{
    [Fact]
    public void Create_WithValidValues_ExposesProperties()
    {
        var record = new MunicipalFinanceRecord(
            BfsNumber.Create(4551), "Aadorf", 2024,
            selfFinancingRatio: 163.81m, netDebtPerCapitaChf: 1415.95m);

        Assert.Equal(4551, record.BfsNumber.Value);
        Assert.Equal("Aadorf", record.MunicipalityName);
        Assert.Equal(2024, record.Year);
        Assert.Equal(163.81m, record.SelfFinancingRatio);
        Assert.Equal(1415.95m, record.NetDebtPerCapitaChf);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void BfsNumber_Create_RejectsNonPositive(int value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => BfsNumber.Create(value));

    [Fact]
    public void Create_WithBlankName_Throws()
        => Assert.Throws<ArgumentException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(1), "  ", 2024, null, null));

    [Fact]
    public void Create_WithYearBefore1900_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MunicipalFinanceRecord(BfsNumber.Create(1), "X", 1899, null, null));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter MunicipalFinanceRecordTests`
Expected: FAIL (compile error — `BfsNumber`/`MunicipalFinanceRecord` not defined).

- [ ] **Step 3: Write minimal implementation**

Create `src/Kantonal.Domain/BfsNumber.cs`:

```csharp
namespace Kantonal.Domain;

public readonly record struct BfsNumber(int Value)
{
    public static BfsNumber Create(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "BFS number must be positive.");
        return new BfsNumber(value);
    }
}
```

Create `src/Kantonal.Domain/MunicipalFinanceRecord.cs`:

```csharp
namespace Kantonal.Domain;

public sealed class MunicipalFinanceRecord
{
    public MunicipalFinanceRecord(
        BfsNumber bfsNumber,
        string municipalityName,
        int year,
        decimal? selfFinancingRatio,
        decimal? netDebtPerCapitaChf)
    {
        if (string.IsNullOrWhiteSpace(municipalityName))
            throw new ArgumentException("Municipality name is required.", nameof(municipalityName));
        if (year < 1900)
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be 1900 or later.");

        BfsNumber = bfsNumber;
        MunicipalityName = municipalityName.Trim();
        Year = year;
        SelfFinancingRatio = selfFinancingRatio;
        NetDebtPerCapitaChf = netDebtPerCapitaChf;
    }

    public BfsNumber BfsNumber { get; }
    public string MunicipalityName { get; }
    public int Year { get; }
    public decimal? SelfFinancingRatio { get; }
    public decimal? NetDebtPerCapitaChf { get; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter MunicipalFinanceRecordTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Domain tests/Kantonal.Tests/Domain
git commit -m "feat(domain): municipal finance record entity and BFS value object"
```

---

### Task 2: Application — repository port, DTO, and paged query service

**Files:**
- Create: `src/Kantonal.Application/FinanceRecordDto.cs`
- Create: `src/Kantonal.Application/PagedResult.cs`
- Create: `src/Kantonal.Application/IFinanceRepository.cs`
- Create: `src/Kantonal.Application/FinanceQueryService.cs`
- Test: `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs`

**Interfaces:**
- Consumes: `MunicipalFinanceRecord`, `BfsNumber` from Domain.
- Produces:
  - `record FinanceRecordDto(int BfsNumber, string MunicipalityName, int Year, decimal? SelfFinancingRatio, decimal? NetDebtPerCapitaChf)`.
  - `record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)`.
  - `interface IFinanceRepository { Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct); Task<int> CountAsync(CancellationToken ct); }`.
  - `class FinanceQueryService(IFinanceRepository repo)` with `Task<PagedResult<FinanceRecordDto>> GetPageAsync(int page, int pageSize, CancellationToken ct)`. Clamps `page>=1`, `pageSize` into `[1,100]`.

- [ ] **Step 1: Write the failing test**

Create `tests/Kantonal.Tests/Application/FinanceQueryServiceTests.cs`:

```csharp
using Kantonal.Application;
using Kantonal.Domain;

namespace Kantonal.Tests.Application;

public class FinanceQueryServiceTests
{
    private sealed class FakeRepo : IFinanceRepository
    {
        private readonly List<MunicipalFinanceRecord> _all;
        public FakeRepo(IEnumerable<MunicipalFinanceRecord> all) => _all = all.ToList();
        public Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MunicipalFinanceRecord>>(_all.Skip(skip).Take(take).ToList());
        public Task<int> CountAsync(CancellationToken ct) => Task.FromResult(_all.Count);
    }

    private static MunicipalFinanceRecord Row(int bfs, string name)
        => new(BfsNumber.Create(bfs), name, 2024, 100m, 50m);

    [Fact]
    public async Task GetPageAsync_ReturnsRequestedPageAndTotal()
    {
        var repo = new FakeRepo(new[] { Row(1, "A"), Row(2, "B"), Row(3, "C") });
        var service = new FinanceQueryService(repo);

        var result = await service.GetPageAsync(page: 2, pageSize: 2, CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Single(result.Items);
        Assert.Equal("C", result.Items[0].MunicipalityName);
    }

    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(-3, 10, 1)]
    public async Task GetPageAsync_ClampsPageToAtLeastOne(int page, int size, int expectedPage)
    {
        var service = new FinanceQueryService(new FakeRepo(Array.Empty<MunicipalFinanceRecord>()));
        var result = await service.GetPageAsync(page, size, CancellationToken.None);
        Assert.Equal(expectedPage, result.Page);
    }

    [Fact]
    public async Task GetPageAsync_ClampsPageSizeToMax100()
    {
        var service = new FinanceQueryService(new FakeRepo(Array.Empty<MunicipalFinanceRecord>()));
        var result = await service.GetPageAsync(1, 9999, CancellationToken.None);
        Assert.Equal(100, result.PageSize);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter FinanceQueryServiceTests`
Expected: FAIL (compile error — types not defined).

- [ ] **Step 3: Write minimal implementation**

Create `src/Kantonal.Application/FinanceRecordDto.cs`:

```csharp
namespace Kantonal.Application;

public record FinanceRecordDto(
    int BfsNumber,
    string MunicipalityName,
    int Year,
    decimal? SelfFinancingRatio,
    decimal? NetDebtPerCapitaChf);
```

Create `src/Kantonal.Application/PagedResult.cs`:

```csharp
namespace Kantonal.Application;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
```

Create `src/Kantonal.Application/IFinanceRepository.cs`:

```csharp
using Kantonal.Domain;

namespace Kantonal.Application;

public interface IFinanceRepository
{
    Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
}
```

Create `src/Kantonal.Application/FinanceQueryService.cs`:

```csharp
using Kantonal.Domain;

namespace Kantonal.Application;

public class FinanceQueryService
{
    private const int MaxPageSize = 100;
    private readonly IFinanceRepository _repo;

    public FinanceQueryService(IFinanceRepository repo) => _repo = repo;

    public async Task<PagedResult<FinanceRecordDto>> GetPageAsync(int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var total = await _repo.CountAsync(ct);
        var records = await _repo.GetAsync((page - 1) * pageSize, pageSize, ct);
        var items = records.Select(ToDto).ToList();

        return new PagedResult<FinanceRecordDto>(items, page, pageSize, total);
    }

    private static FinanceRecordDto ToDto(MunicipalFinanceRecord r) => new(
        r.BfsNumber.Value, r.MunicipalityName, r.Year, r.SelfFinancingRatio, r.NetDebtPerCapitaChf);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter FinanceQueryServiceTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Kantonal.Application tests/Kantonal.Tests/Application
git commit -m "feat(application): paged finance query service over repository port"
```

---

### Task 3: Infrastructure — EF Core DbContext, repository, seed, migration

**Files:**
- Modify: `src/Kantonal.Infrastructure/Kantonal.Infrastructure.csproj` (add EF/Npgsql packages)
- Create: `src/Kantonal.Infrastructure/KantonalDbContext.cs`
- Create: `src/Kantonal.Infrastructure/EfFinanceRepository.cs`
- Create: `src/Kantonal.Infrastructure/DependencyInjection.cs`
- Create: `src/Kantonal.Infrastructure/Migrations/*` (generated)
- Test: `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`
- Modify: `tests/Kantonal.Tests/Kantonal.Tests.csproj` (add EF InMemory provider for the repo test)

**Interfaces:**
- Consumes: `MunicipalFinanceRecord`, `BfsNumber` (Domain); `IFinanceRepository` (Application).
- Produces:
  - `KantonalDbContext(DbContextOptions<KantonalDbContext>)` exposing `DbSet<MunicipalFinanceRecord> FinanceRecords`, mapped to table `finance_records` with composite key `(BfsNumber.Value, Year)`.
  - `EfFinanceRepository(KantonalDbContext) : IFinanceRepository` ordering by `MunicipalityName, Year`.
  - `static IServiceCollection AddInfrastructure(this IServiceCollection, string connectionString)` registering the DbContext (Npgsql) and `IFinanceRepository`.

- [ ] **Step 1: Add EF Core packages**

```bash
cd /home/reblayzer/dev/kantonal
dotnet add src/Kantonal.Infrastructure package Microsoft.EntityFrameworkCore --version 8.0.8
dotnet add src/Kantonal.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.8
dotnet add src/Kantonal.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 8.0.8
dotnet add tests/Kantonal.Tests package Microsoft.EntityFrameworkCore.InMemory --version 8.0.8
```

Install the EF CLI if missing:

```bash
dotnet tool install --global dotnet-ef --version 8.0.8 || dotnet tool update --global dotnet-ef --version 8.0.8
```

- [ ] **Step 2: Write the failing repository test**

Create `tests/Kantonal.Tests/Infrastructure/EfFinanceRepositoryTests.cs`:

```csharp
using Kantonal.Domain;
using Kantonal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Tests.Infrastructure;

public class EfFinanceRepositoryTests
{
    private static KantonalDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<KantonalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new KantonalDbContext(options);
    }

    [Fact]
    public async Task GetAsync_OrdersByNameAndPaginates()
    {
        await using var ctx = NewContext();
        ctx.FinanceRecords.AddRange(
            new MunicipalFinanceRecord(BfsNumber.Create(2), "Bravo", 2024, 1m, 1m),
            new MunicipalFinanceRecord(BfsNumber.Create(1), "Alpha", 2024, 2m, 2m),
            new MunicipalFinanceRecord(BfsNumber.Create(3), "Charlie", 2024, 3m, 3m));
        await ctx.SaveChangesAsync();

        var repo = new EfFinanceRepository(ctx);
        var page = await repo.GetAsync(skip: 1, take: 1, CancellationToken.None);
        var total = await repo.CountAsync(CancellationToken.None);

        Assert.Equal(3, total);
        Assert.Single(page);
        Assert.Equal("Bravo", page[0].MunicipalityName);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter EfFinanceRepositoryTests`
Expected: FAIL (compile error — `KantonalDbContext`/`EfFinanceRepository` not defined).

- [ ] **Step 4: Implement the DbContext, mapping, and repository**

Create `src/Kantonal.Infrastructure/KantonalDbContext.cs`:

```csharp
using Kantonal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Infrastructure;

public class KantonalDbContext : DbContext
{
    public KantonalDbContext(DbContextOptions<KantonalDbContext> options) : base(options) { }

    public DbSet<MunicipalFinanceRecord> FinanceRecords => Set<MunicipalFinanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MunicipalFinanceRecord>();
        entity.ToTable("finance_records");

        entity.Property(e => e.BfsNumber)
            .HasColumnName("bfs_number")
            .HasConversion(v => v.Value, v => BfsNumber.Create(v));

        entity.Property(e => e.Year).HasColumnName("year");
        entity.HasKey(e => new { e.BfsNumber, e.Year });

        entity.Property(e => e.MunicipalityName).HasColumnName("municipality_name").IsRequired();
        entity.Property(e => e.SelfFinancingRatio).HasColumnName("self_financing_ratio").HasColumnType("numeric");
        entity.Property(e => e.NetDebtPerCapitaChf).HasColumnName("net_debt_per_capita_chf").HasColumnType("numeric");
    }
}
```

> Mapping notes for the implementer:
> - `BfsNumber` is a `record struct` with value equality, so it works as part of the composite key `(BfsNumber, Year)` in both Npgsql and the InMemory provider used by the test.
> - The entity has no parameterless constructor; EF Core 8 binds the constructor by parameter name. Our constructor parameter names (`bfsNumber`, `municipalityName`, `year`, `selfFinancingRatio`, `netDebtPerCapitaChf`) match the property names case-insensitively, so EF can materialize it — keep that alignment.
> - If the value-converted key genuinely cannot be made to work with the InMemory provider, that is a real blocker: report it (do not weaken the test). A valid fallback is to store `bfs_number` as a plain `int` property and expose `BfsNumber` as a domain wrapper, but only take that route if the converter approach fails and note it in your report.

Create `src/Kantonal.Infrastructure/EfFinanceRepository.cs`:

```csharp
using Kantonal.Application;
using Kantonal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Infrastructure;

public class EfFinanceRepository : IFinanceRepository
{
    private readonly KantonalDbContext _db;
    public EfFinanceRepository(KantonalDbContext db) => _db = db;

    public async Task<IReadOnlyList<MunicipalFinanceRecord>> GetAsync(int skip, int take, CancellationToken ct)
        => await _db.FinanceRecords
            .OrderBy(r => r.MunicipalityName).ThenBy(r => r.Year)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct) => _db.FinanceRecords.CountAsync(ct);
}
```

Create `src/Kantonal.Infrastructure/DependencyInjection.cs`:

```csharp
using Kantonal.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kantonal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<KantonalDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IFinanceRepository, EfFinanceRepository>();
        return services;
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter EfFinanceRepositoryTests`
Expected: PASS (1 test). If the value-object key fails under the InMemory provider, that is a mapping bug — fix the mapping, do not weaken the test.

- [ ] **Step 6: Create the initial migration**

```bash
cd /home/reblayzer/dev/kantonal
dotnet ef migrations add InitialCreate \
  --project src/Kantonal.Infrastructure \
  --startup-project src/Kantonal.Api \
  --output-dir Migrations
```

Expected: a `Migrations/` folder with `*_InitialCreate.cs` creating table `finance_records`.
(Requires Task 4's `Program.cs` to compile; if running tasks strictly in order, do this step after Task 4 Step 4 and fold its commit into Task 4. Otherwise add a temporary design-time factory — see note.)

> If you need the migration before the API is wired, add `src/Kantonal.Infrastructure/KantonalDbContextFactory.cs`:
> ```csharp
> using Microsoft.EntityFrameworkCore;
> using Microsoft.EntityFrameworkCore.Design;
> namespace Kantonal.Infrastructure;
> public class KantonalDbContextFactory : IDesignTimeDbContextFactory<KantonalDbContext>
> {
>     public KantonalDbContext CreateDbContext(string[] args)
>     {
>         var options = new DbContextOptionsBuilder<KantonalDbContext>()
>             .UseNpgsql("Host=localhost;Database=kantonal;Username=postgres;Password=postgres").Options;
>         return new KantonalDbContext(options);
>     }
> }
> ```

- [ ] **Step 7: Commit**

```bash
git add src/Kantonal.Infrastructure tests/Kantonal.Tests/Infrastructure tests/Kantonal.Tests/Kantonal.Tests.csproj
git commit -m "feat(infrastructure): EF Core context, repository, and initial migration"
```

---

### Task 4: Api — minimal API endpoint with envelope, DI wiring, seed-on-start

**Files:**
- Modify: `src/Kantonal.Api/Program.cs` (replace template)
- Modify: `src/Kantonal.Api/appsettings.json` (connection string + seed flag)
- Create: `src/Kantonal.Api/ApiEnvelope.cs`
- Create: `src/Kantonal.Api/DatabaseSeeder.cs`
- Modify: `src/Kantonal.Api/Kantonal.Api.csproj` (add Swashbuckle)
- Test: `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`
- Modify: `tests/Kantonal.Tests/Kantonal.Tests.csproj` (add Mvc.Testing)

**Interfaces:**
- Consumes: `FinanceQueryService`, `PagedResult<FinanceRecordDto>`, `FinanceRecordDto` (Application); `AddInfrastructure`, `KantonalDbContext` (Infrastructure).
- Produces:
  - HTTP `GET /api/finance?page=&pageSize=` returning `200` with body `{ ok: true, data: { items, page, pageSize, total } }`.
  - A public partial `Program` class so `WebApplicationFactory<Program>` can host it in tests.
  - `DatabaseSeeder.SeedAsync(KantonalDbContext)` inserting a fixed set of ≥3 Thurgau rows if the table is empty.

- [ ] **Step 1: Add API packages**

```bash
cd /home/reblayzer/dev/kantonal
dotnet add src/Kantonal.Api package Swashbuckle.AspNetCore --version 6.6.2
dotnet add src/Kantonal.Api reference src/Kantonal.Application
dotnet add tests/Kantonal.Tests package Microsoft.AspNetCore.Mvc.Testing --version 8.0.8
```

Register `FinanceQueryService` in Application via a DI extension — create `src/Kantonal.Application/DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Kantonal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<FinanceQueryService>();
        return services;
    }
}
```

> This requires `Microsoft.Extensions.DependencyInjection.Abstractions`. Add it:
> `dotnet add src/Kantonal.Application package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2`

- [ ] **Step 2: Write the failing endpoint test**

Create `tests/Kantonal.Tests/Api/FinanceEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Kantonal.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kantonal.Tests.Api;

public class FinanceEndpointTests : IClassFixture<FinanceEndpointTests.TestApi>
{
    private readonly TestApi _api;
    public FinanceEndpointTests(TestApi api) => _api = api;

    [Fact]
    public async Task GetFinance_ReturnsOkEnvelopeWithItems()
    {
        var client = _api.CreateClient();
        var response = await client.GetAsync("/api/finance?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Envelope>();
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.True(body.Data!.Total >= 3);
        Assert.NotEmpty(body.Data.Items);
    }

    public record Envelope(bool Ok, Data? Data);
    public record Data(List<Item> Items, int Page, int PageSize, int Total);
    public record Item(int BfsNumber, string MunicipalityName, int Year);

    public class TestApi : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<KantonalDbContext>));
                services.Remove(descriptor);
                services.AddDbContext<KantonalDbContext>(o => o.UseInMemoryDatabase("api-tests"));
            });
        }
    }
}
```

> Add the InMemory provider to the test project if not already present (Task 3 added it).

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter FinanceEndpointTests`
Expected: FAIL (compile error — `Program` not public / endpoint missing).

- [ ] **Step 4: Implement the API**

Replace `src/Kantonal.Api/Program.cs`:

```csharp
using Kantonal.Api;
using Kantonal.Application;
using Kantonal.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Kantonal")
    ?? "Host=localhost;Database=kantonal;Username=postgres;Password=postgres";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/finance", async (FinanceQueryService service, int? page, int? pageSize, CancellationToken ct) =>
{
    var result = await service.GetPageAsync(page ?? 1, pageSize ?? 20, ct);
    return Results.Ok(ApiEnvelope.Success(result));
});

app.MapGet("/health", () => Results.Ok(ApiEnvelope.Success(new { status = "ok" })));

// Apply migrations + seed only when using a relational provider (skipped under InMemory tests).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KantonalDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

app.Run();

public partial class Program { }
```

Create `src/Kantonal.Api/ApiEnvelope.cs`:

```csharp
namespace Kantonal.Api;

public static class ApiEnvelope
{
    public static object Success(object data) => new { ok = true, data };
    public static object Error(string code, string message) => new { ok = false, error = new { code, message } };
}
```

Create `src/Kantonal.Api/DatabaseSeeder.cs`:

```csharp
using Kantonal.Domain;
using Kantonal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kantonal.Api;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(KantonalDbContext db)
    {
        if (await db.FinanceRecords.AnyAsync()) return;

        db.FinanceRecords.AddRange(
            new MunicipalFinanceRecord(BfsNumber.Create(4551), "Aadorf", 2024, 163.81m, 1415.95m),
            new MunicipalFinanceRecord(BfsNumber.Create(4711), "Affeltrangen", 2024, 80.36m, -683.62m),
            new MunicipalFinanceRecord(BfsNumber.Create(4486), "Amlikon-Bissegg", 2024, 95.10m, 210.40m));

        await db.SaveChangesAsync();
    }
}
```

Set `src/Kantonal.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Kantonal": "Host=localhost;Database=kantonal;Username=postgres;Password=postgres"
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter FinanceEndpointTests`
Expected: PASS (1 test).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test Kantonal.sln`
Expected: PASS (all tasks' tests green).

- [ ] **Step 7: Commit**

```bash
git add src/Kantonal.Api src/Kantonal.Application/DependencyInjection.cs tests/Kantonal.Tests/Api tests/Kantonal.Tests/Kantonal.Tests.csproj
git commit -m "feat(api): GET /api/finance with response envelope, seed, and migration on start"
```

---

### Task 5: Web — Blazor page that reads the API and renders a table

**Files:**
- Create: `src/Kantonal.Web/Services/FinanceApiClient.cs`
- Create: `src/Kantonal.Web/Models/FinanceModels.cs`
- Modify: `src/Kantonal.Web/Program.cs` (register typed HttpClient)
- Create: `src/Kantonal.Web/Components/Pages/Finance.razor`
- Modify: `src/Kantonal.Web/appsettings.json` (API base URL)
- Test: `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs`

**Interfaces:**
- Consumes: the API's `GET /api/finance` envelope shape.
- Produces:
  - `record FinanceRow(int BfsNumber, string MunicipalityName, int Year, decimal? SelfFinancingRatio, decimal? NetDebtPerCapitaChf)`.
  - `record FinancePage(IReadOnlyList<FinanceRow> Items, int Page, int PageSize, int Total)`.
  - `FinanceApiClient(HttpClient)` with `Task<FinancePage> GetAsync(int page, int pageSize, CancellationToken ct)` that unwraps the `{ ok, data }` envelope.

- [ ] **Step 1: Write the failing client test**

Create `tests/Kantonal.Tests/Web/FinanceApiClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using Kantonal.Web.Services;

namespace Kantonal.Tests.Web;

public class FinanceApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public StubHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task GetAsync_UnwrapsEnvelopeIntoRows()
    {
        const string json = """
        {"ok":true,"data":{"items":[
          {"bfsNumber":4551,"municipalityName":"Aadorf","year":2024,"selfFinancingRatio":163.81,"netDebtPerCapitaChf":1415.95}
        ],"page":1,"pageSize":20,"total":3}}
        """;
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("http://api.test") };
        var client = new FinanceApiClient(http);

        var page = await client.GetAsync(1, 20, CancellationToken.None);

        Assert.Equal(3, page.Total);
        Assert.Single(page.Items);
        Assert.Equal("Aadorf", page.Items[0].MunicipalityName);
        Assert.Equal(163.81m, page.Items[0].SelfFinancingRatio);
    }
}
```

Add the Web reference to the test project:

```bash
dotnet add tests/Kantonal.Tests reference src/Kantonal.Web
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Kantonal.Tests --filter FinanceApiClientTests`
Expected: FAIL (compile error — `FinanceApiClient` not defined).

- [ ] **Step 3: Implement models and client**

Create `src/Kantonal.Web/Models/FinanceModels.cs`:

```csharp
namespace Kantonal.Web.Models;

public record FinanceRow(
    int BfsNumber,
    string MunicipalityName,
    int Year,
    decimal? SelfFinancingRatio,
    decimal? NetDebtPerCapitaChf);

public record FinancePage(IReadOnlyList<FinanceRow> Items, int Page, int PageSize, int Total);
```

Create `src/Kantonal.Web/Services/FinanceApiClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Kantonal.Web.Models;

namespace Kantonal.Web.Services;

public class FinanceApiClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    public FinanceApiClient(HttpClient http) => _http = http;

    private record Envelope(bool Ok, FinancePage? Data);

    public async Task<FinancePage> GetAsync(int page, int pageSize, CancellationToken ct)
    {
        var envelope = await _http.GetFromJsonAsync<Envelope>(
            $"/api/finance?page={page}&pageSize={pageSize}", JsonOptions, ct);

        if (envelope is not { Ok: true, Data: not null })
            throw new InvalidOperationException("Finance API returned an unsuccessful response.");

        return envelope.Data;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Kantonal.Tests --filter FinanceApiClientTests`
Expected: PASS (1 test).

- [ ] **Step 5: Wire the typed client and build the page**

In `src/Kantonal.Web/Program.cs`, after `builder.Services.AddRazorComponents()...`, add:

```csharp
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:8080";
builder.Services.AddHttpClient<Kantonal.Web.Services.FinanceApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl));
```

Set `src/Kantonal.Web/appsettings.json` to include:

```json
{
  "ApiBaseUrl": "http://localhost:8080",
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

Create `src/Kantonal.Web/Components/Pages/Finance.razor`:

```razor
@page "/finance"
@using Kantonal.Web.Models
@using Kantonal.Web.Services
@inject FinanceApiClient Api
@rendermode InteractiveServer

<PageTitle>Kantonal — Municipal Finances</PageTitle>

<h1>Cantonal/Municipal Finances (Thurgau)</h1>

@if (_page is null)
{
    <p><em>Loading…</em></p>
}
else
{
    <p>@_page.Total record(s).</p>
    <table class="table">
        <thead>
            <tr><th>BFS</th><th>Municipality</th><th>Year</th><th>Self-financing %</th><th>Net debt/capita CHF</th></tr>
        </thead>
        <tbody>
            @foreach (var row in _page.Items)
            {
                <tr>
                    <td>@row.BfsNumber</td>
                    <td>@row.MunicipalityName</td>
                    <td>@row.Year</td>
                    <td>@row.SelfFinancingRatio</td>
                    <td>@row.NetDebtPerCapitaChf</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private FinancePage? _page;

    protected override async Task OnInitializedAsync()
        => _page = await Api.GetAsync(1, 20, CancellationToken.None);
}
```

Add a nav link in `src/Kantonal.Web/Components/Layout/NavMenu.razor` (a `<div class="nav-item">` with `<NavLink href="finance">Finances</NavLink>`), matching the template's existing nav markup.

- [ ] **Step 6: Build the web project**

Run: `dotnet build src/Kantonal.Web`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add src/Kantonal.Web tests/Kantonal.Tests/Web
git commit -m "feat(web): Blazor finance page reading the API via typed HttpClient"
```

---

### Task 6: Docker — Dockerfiles and docker compose for api + db + web

**Files:**
- Create: `src/Kantonal.Api/Dockerfile`
- Create: `src/Kantonal.Web/Dockerfile`
- Create: `.dockerignore`
- Create: `docker-compose.yml`
- Create: `README.md` (run instructions; expanded by later plans)

**Interfaces:**
- Consumes: the built `Kantonal.Api` (listens on `:8080`) and `Kantonal.Web` (listens on `:8080`, env `ApiBaseUrl`).
- Produces: `docker compose up` serving the API on host `:8080`, Blazor on host `:5080`, Postgres on `:5432`.

- [ ] **Step 1: Add a .dockerignore**

Create `.dockerignore`:

```gitignore
**/bin/
**/obj/
.git/
.vs/
*.user
```

- [ ] **Step 2: API Dockerfile**

Create `src/Kantonal.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Kantonal.sln ./
COPY src/Kantonal.Domain/Kantonal.Domain.csproj src/Kantonal.Domain/
COPY src/Kantonal.Application/Kantonal.Application.csproj src/Kantonal.Application/
COPY src/Kantonal.Infrastructure/Kantonal.Infrastructure.csproj src/Kantonal.Infrastructure/
COPY src/Kantonal.Api/Kantonal.Api.csproj src/Kantonal.Api/
RUN dotnet restore src/Kantonal.Api/Kantonal.Api.csproj
COPY . .
RUN dotnet publish src/Kantonal.Api/Kantonal.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kantonal.Api.dll"]
```

- [ ] **Step 3: Web Dockerfile**

Create `src/Kantonal.Web/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Kantonal.sln ./
COPY src/Kantonal.Web/Kantonal.Web.csproj src/Kantonal.Web/
RUN dotnet restore src/Kantonal.Web/Kantonal.Web.csproj
COPY . .
RUN dotnet publish src/Kantonal.Web/Kantonal.Web.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kantonal.Web.dll"]
```

- [ ] **Step 4: docker compose**

Create `docker-compose.yml`:

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_DB: kantonal
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d kantonal"]
      interval: 5s
      timeout: 3s
      retries: 10
    volumes:
      - kantonal-db:/var/lib/postgresql/data

  api:
    build:
      context: .
      dockerfile: src/Kantonal.Api/Dockerfile
    environment:
      ConnectionStrings__Kantonal: "Host=db;Database=kantonal;Username=postgres;Password=postgres"
    ports:
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy

  web:
    build:
      context: .
      dockerfile: src/Kantonal.Web/Dockerfile
    environment:
      ApiBaseUrl: "http://api:8080"
    ports:
      - "5080:8080"
    depends_on:
      - api

volumes:
  kantonal-db:
```

- [ ] **Step 5: Bring the stack up and verify end-to-end**

```bash
cd /home/reblayzer/dev/kantonal
docker compose up --build -d
# wait for api, then:
curl -s http://localhost:8080/api/finance?page=1&pageSize=5
```

Expected: JSON `{"ok":true,"data":{"items":[...],"page":1,"pageSize":5,"total":3}}` with the three seeded Thurgau rows.
Then open `http://localhost:5080/finance` in a browser — the table shows the seeded rows.

```bash
docker compose down
```

- [ ] **Step 6: Write a minimal README**

Create `README.md`:

```markdown
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

## Run tests

```bash
dotnet test Kantonal.sln
```

## Architecture

`Domain` ← `Application` ← `Infrastructure` (EF Core/Postgres) + `Api` (composition
root). `Web` (Blazor) is a separate client calling the API over HTTP.

Built with AI-assisted development (Claude Code) and reviewed before merging.
Azure deployment notes and the opendata.swiss import job are tracked in follow-up work.
```

- [ ] **Step 7: Commit**

```bash
git add .dockerignore src/Kantonal.Api/Dockerfile src/Kantonal.Web/Dockerfile docker-compose.yml README.md
git commit -m "feat(docker): compose api, db, and web for one-command startup"
```

---

## Follow-up plans (out of scope for this skeleton)

1. **Import job:** typed `HttpClient` against `https://data.tg.ch/api/v2/catalog/datasets/sk-stat-4/records` (offset/limit pagination, `total_count`), mapping all 9 KPI ratios; idempotent upsert keyed on `(BfsNumber, Year)`; a hosted/scheduled importer; replace the static seeder.
2. **Full API + domain:** all nine financial indicators on the entity; filter by canton/municipality/year and sort; single-record endpoint `GET /api/finance/{bfs}/{year}`; typed errors → HTTP codes.
3. **Dashboard:** filter/sort controls and one chart.
4. **CI/CD:** GitHub Actions (restore, build, test, `dotnet format --verify-no-changes`); Azure App Service + Azure Database for PostgreSQL deployment notes in the README.

## Self-Review

- **Spec coverage (this skeleton):** clean-architecture solution (Task 0) ✓; Domain entity/value object (Task 1) ✓; Application services + DTOs + validation (Task 2) ✓; EF Core + migration (Task 3) ✓; REST API + Swagger + envelope (Task 4) ✓; Blazor dashboard table (Task 5) ✓; docker compose api+db+web (Task 6) ✓; xUnit unit + API integration tests (Tasks 1–5) ✓. Deferred to follow-up plans (noted above): import job, full filtering/sort/single-record, chart, GitHub Actions CI, Azure notes — these are explicitly out of the walking-skeleton scope.
- **Placeholder scan:** no TBD/TODO; the one design caveat (EF value-object key mapping in Task 3) ships with the corrected mapping inline, not a placeholder.
- **Type consistency:** `IFinanceRepository.GetAsync(int skip,int take,CancellationToken)` / `CountAsync(CancellationToken)` consistent across Tasks 2–3; `FinanceQueryService.GetPageAsync(page,pageSize,ct)` consistent across Tasks 2 and 4; envelope `{ ok, data:{ items,page,pageSize,total } }` consistent across Tasks 4 and 5; `FinanceRecordDto` property names match the Web `FinanceRow` JSON (web-defaults camelCase) used in Task 5's test.
