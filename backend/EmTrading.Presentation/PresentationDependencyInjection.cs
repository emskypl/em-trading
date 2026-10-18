using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Presentation.Analytics;
using EmTrading.Presentation.Backtesting;
using EmTrading.Presentation.Jobs;
using EmTrading.Presentation.Logging;
using EmTrading.Presentation.MarketData;
using EmTrading.Presentation.Optimization;
using EmTrading.Presentation.Settings;
using EmTrading.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.Presentation;

public static class PresentationDependencyInjection
{
    /// <summary>
    /// View models. The host must also register <see cref="UserSettings"/>, <see cref="IUserSettingsStore"/> and
    /// <see cref="AppEnvironmentInfo"/>. Pages are singletons so their state survives navigation.
    /// </summary>
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<LogFeed>();

        services.AddSingleton<BacktestViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<OptimizationViewModel>();
        services.AddSingleton<AnalysisViewModel>();
        services.AddSingleton<MarketDataViewModel>();
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}
