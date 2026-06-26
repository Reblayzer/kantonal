using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kantonal.Infrastructure;

public class KantonalDbContextFactory : IDesignTimeDbContextFactory<KantonalDbContext>
{
    public KantonalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KantonalDbContext>()
            .UseNpgsql("Host=localhost;Database=kantonal;Username=postgres;Password=postgres").Options;
        return new KantonalDbContext(options);
    }
}
