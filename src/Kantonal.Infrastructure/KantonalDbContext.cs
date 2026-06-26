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

        // Seven additional HRM2 ratios — column names and migration are added in Task 2.
        entity.Property(e => e.SelfFinancingShare);
        entity.Property(e => e.InterestBurdenShare);
        entity.Property(e => e.CapitalServiceShare);
        entity.Property(e => e.InvestmentShare);
        entity.Property(e => e.GrossDebtShare);
        entity.Property(e => e.NetDebtQuotient);
        entity.Property(e => e.BalanceSheetSurplusQuotient);

        entity.Ignore(e => e.Indicators);
    }
}
