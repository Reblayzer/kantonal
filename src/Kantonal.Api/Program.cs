using Kantonal.Api;
using Kantonal.Application;
using Kantonal.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Kantonal")
    ?? "Host=localhost;Database=kantonal;Username=postgres;Password=postgres";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddHttpClient<IFinanceImportSource, ThurgauFinanceImporter>(client =>
{
    client.BaseAddress = new Uri("https://data.tg.ch/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<FinanceImportService>();
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

// Dev-only manual import trigger. No auth yet — authorization is a follow-up (see PROJECT_BRAINSTORM.md).
app.MapPost("/api/import", async (FinanceImportService importer, CancellationToken ct) =>
{
    var imported = await importer.ImportAsync(ct);
    return Results.Ok(ApiEnvelope.Success(new { imported }));
});

// Apply migrations (relational only) and import finance data. A failed import must not crash startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KantonalDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();

    try
    {
        var importer = scope.ServiceProvider.GetRequiredService<FinanceImportService>();
        var imported = await importer.ImportAsync(CancellationToken.None);
        app.Logger.LogInformation("Startup finance import upserted {Count} records.", imported);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Startup finance import failed; continuing without fresh data.");
    }
}

app.Run();

public partial class Program { }
