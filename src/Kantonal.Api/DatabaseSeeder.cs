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
