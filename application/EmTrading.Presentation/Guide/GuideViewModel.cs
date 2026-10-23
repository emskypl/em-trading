using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Presentation.Analytics;
using EmTrading.Presentation.Backtesting;
using EmTrading.Presentation.Jobs;
using EmTrading.Presentation.MarketData;
using EmTrading.Presentation.Optimization;
using EmTrading.Presentation.Settings;
using EmTrading.Presentation.Shell;
using EmTrading.Presentation.Trading;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation.Guide;

/// <summary>
/// How to use the app, one section per task in the order a new user meets them. The texts are resources
/// (<c>Guide_{Id}_Title</c> / <c>Guide_{Id}_Body</c>), so they follow the language setting.
/// </summary>
public sealed partial class GuideViewModel(IMessenger messenger, StatusViewModel status, ILogger<GuideViewModel> logger)
    : PageViewModel(status, logger)
{
    public IReadOnlyList<GuideSection> Sections { get; } =
    [
        new("Start", typeof(SettingsViewModel)),
        new("MarketData", typeof(MarketDataViewModel)),
        new("Backtest", typeof(BacktestViewModel)),
        new("History", typeof(HistoryViewModel)),
        new("Compare", typeof(Compare.CompareViewModel)),
        new("Optimization", typeof(OptimizationViewModel)),
        new("Analysis", typeof(AnalysisViewModel)),
        new("Assistant", typeof(Assistant.AssistantViewModel)),
        new("Trading", typeof(TradingViewModel)),
        new("Jobs", typeof(JobsViewModel)),
        new("Symbols", null)
    ];

    [RelayCommand]
    private void OpenPage(Type? pageType)
    {
        if (pageType is not null)
            messenger.Send(new NavigateMessage(pageType));
    }
}

/// <param name="PageType">The page the section describes, opened by its button; <c>null</c> for general tips.</param>
public sealed record GuideSection(string Id, Type? PageType)
{
    public string TitleKey => $"Guide_{Id}_Title";

    public string BodyKey => $"Guide_{Id}_Body";

    public bool HasPage => PageType is not null;
}
