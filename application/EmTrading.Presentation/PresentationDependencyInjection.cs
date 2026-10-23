using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Presentation.Analytics;
using EmTrading.Application.Assistant;
using EmTrading.Presentation.Assistant;
using EmTrading.Presentation.Backtesting;
using EmTrading.Presentation.Compare;
using EmTrading.Presentation.Guide;
using EmTrading.Presentation.Instruments;
using EmTrading.Presentation.Jobs;
using EmTrading.Presentation.Logging;
using EmTrading.Presentation.MarketData;
using EmTrading.Presentation.Optimization;
using EmTrading.Presentation.Settings;
using EmTrading.Presentation.Shell;
using EmTrading.Presentation.Trading;
using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.Presentation;

public static class PresentationDependencyInjection
{
    /// <summary>
    /// View models. The host must also register <see cref="UserSettings"/>, <see cref="IUserSettingsStore"/>,
    /// <see cref="AppEnvironmentInfo"/> and <see cref="Common.IUserConfirmation"/>. Pages are singletons so their state survives navigation.
    /// </summary>
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<LogFeed>();
        services.AddSingleton<InstrumentCatalog>();
        services.AddSingleton<DownloadedSymbolCatalog>();

        services.AddSingleton<BacktestViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<CompareViewModel>();
        services.AddSingleton<AssistantViewModel>();
        services.AddSingleton<IAssistantActions>(sp => sp.GetRequiredService<AssistantViewModel>());
        services.AddSingleton<OptimizationViewModel>();
        services.AddSingleton<AnalysisViewModel>();
        services.AddSingleton<TradingViewModel>();
        services.AddSingleton<MarketDataViewModel>();
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<GuideViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}
