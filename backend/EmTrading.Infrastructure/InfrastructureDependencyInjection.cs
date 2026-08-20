using EmTrading.Application.Interfaces;
using EmTrading.Infrastructure.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Rejestracja bazy danych i repozytoriów
        // services.AddDbContext<AppDbContext>(...);
        // services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddSingleton<ITradingEngineService, LeanTradingEngineService>();
        return services;
    }
}