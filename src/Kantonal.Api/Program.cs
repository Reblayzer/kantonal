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

var app = builder.Build();

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
