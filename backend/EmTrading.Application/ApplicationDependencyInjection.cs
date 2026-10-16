using System.Reflection;
using EmTrading.Application.Abstractions.Events;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Behaviors;
using EmTrading.Application.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.Application;

public static class ApplicationDependencyInjection
{
    /// <summary>
    /// Requires a <see cref="TimeProvider"/> and a <c>HybridCache</c> implementation, which the host or Infrastructure registers.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationDependencyInjection).Assembly;

        services.AddSingleton<IDispatcher, Dispatcher>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

        // Order matters: logging wraps validation, which wraps caching, which wraps the handler.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Transient, includeInternalTypes: true);
        services.AddClosedImplementationsOf(typeof(IRequestHandler<,>), assembly);
        services.AddClosedImplementationsOf(typeof(IDomainEventHandler<>), assembly);

        return services;
    }

    /// <summary>
    /// Registers every concrete type in <paramref name="assembly"/> as each closed form of <paramref name="openInterface"/> it implements.
    /// </summary>
    private static void AddClosedImplementationsOf(this IServiceCollection services, Type openInterface, Assembly assembly)
    {
        var registrations =
            from type in assembly.GetTypes()
            where type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            from implemented in type.GetInterfaces()
            where implemented.IsGenericType && implemented.GetGenericTypeDefinition() == openInterface
            select (Service: implemented, Implementation: type);

        foreach (var (service, implementation) in registrations)
            services.AddTransient(service, implementation);
    }
}
