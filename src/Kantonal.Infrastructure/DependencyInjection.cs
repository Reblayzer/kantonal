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
