using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.Presentation;

public static class PresentationDependencyInjection
{
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddTransient<MainViewModel>();
        return services;
    }
}
