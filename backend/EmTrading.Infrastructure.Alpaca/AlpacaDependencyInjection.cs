using EmTrading.Infrastructure.Http;
using EmTrading.Infrastructure.MarketData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure.Alpaca;

public static class AlpacaDependencyInjection
{
    /// <summary>
    /// Alpaca as the <see cref="IHistoricalBarSource"/>.
    /// </summary>
    public static IServiceCollection AddAlpacaMarketData(this IServiceCollection services, IConfiguration configuration)
    {
        // Credentials are optional at startup so backtests still work without them; the client checks them on use.
        services.AddOptions<AlpacaOptions>()
            .Bind(configuration.GetSection(AlpacaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<AlpacaRateLimiter>();
        services.AddTransient<AlpacaRateLimitingHandler>();

        // Outer to inner: resilience (timeouts, retries, circuit breaker), then the rate limiter,
        // so every attempt including retries waits for a token.
        var client = services.AddHttpClient<IHistoricalBarSource, AlpacaMarketDataClient>((sp, http) =>
            {
                var options = sp.GetRequiredService<IOptions<AlpacaOptions>>().Value;
                http.BaseAddress = new Uri(options.DataBaseUrl.TrimEnd('/') + "/");
            })
            .ConfigurePrimaryHttpMessageHandler(HttpClientDefaults.CreatePrimaryHandler);
        client.AddStandardResilienceHandler(HttpClientDefaults.ConfigureResilience);
        client.AddHttpMessageHandler<AlpacaRateLimitingHandler>();

        return services;
    }
}
