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
