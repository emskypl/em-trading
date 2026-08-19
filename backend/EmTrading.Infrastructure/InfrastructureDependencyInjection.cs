using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        // Rejestracja bazy danych i repozytoriów
        // services.AddDbContext<AppDbContext>(...);
        // services.AddScoped<IUserRepository, SqlUserRepository>();

        return services;
    }
}