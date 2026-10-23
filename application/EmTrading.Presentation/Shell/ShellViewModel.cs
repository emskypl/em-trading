using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Presentation.Analytics;
using EmTrading.Presentation.Assistant;
using EmTrading.Presentation.Backtesting;
using EmTrading.Presentation.Compare;
using EmTrading.Presentation.Guide;
using EmTrading.Presentation.Jobs;
using EmTrading.Presentation.Localization;
using EmTrading.Presentation.Logging;
using EmTrading.Presentation.MarketData;
using EmTrading.Presentation.Optimization;
using EmTrading.Presentation.Settings;
using EmTrading.Presentation.Trading;

namespace EmTrading.Presentation.Shell;

/// <summary>
/// Main window: sidebar navigation between page view models (each rendered through a DataTemplate), plus the shared
/// status bar and the number of active jobs.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IRecipient<NavigateMessage>
{
    public ShellViewModel(
        BacktestViewModel backtest,
        HistoryViewModel history,
        CompareViewModel compare,
        AssistantViewModel assistant,
        OptimizationViewModel optimization,
        AnalysisViewModel analysis,
        TradingViewModel trading,
        MarketDataViewModel marketData,
        JobsViewModel jobs,
        LogsViewModel logs,
        GuideViewModel guide,
        SettingsViewModel settings,
        StatusViewModel status,
        IMessenger messenger)
    {
        Status = status;
        Jobs = jobs;
        Pages =
        [
            new NavigationItem("Nav_MarketData", "", marketData),
            new NavigationItem("Nav_Backtest", "", backtest),
            new NavigationItem("Nav_History", "", history),
            new NavigationItem("Nav_Compare", "", compare),
            new NavigationItem("Nav_Optimization", "", optimization),
            new NavigationItem("Nav_Analysis", "", analysis),
            new NavigationItem("Nav_Assistant", "", assistant),
            new NavigationItem("Nav_Trading", "", trading),
            new NavigationItem("Nav_Jobs", "", jobs),
            new NavigationItem("Nav_Logs", "", logs),
            new NavigationItem("Nav_Guide", "", guide),
            new NavigationItem("Nav_Settings", "", settings)
        ];

        messenger.RegisterAll(this);
        Loc.Instance.PropertyChanged += OnLanguageChanged;
        SelectedItem = Pages[0];
    }

    public IReadOnlyList<NavigationItem> Pages { get; }

    public StatusViewModel Status { get; }

    public JobsViewModel Jobs { get; }

    public XmlLanguage Language => Loc.Instance.XmlLanguage;

    [ObservableProperty] private NavigationItem? _selectedItem;
    [ObservableProperty] private PageViewModel? _currentPage;

    public string Title => Loc.Get("App_Title");

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        CurrentPage = value?.Page;
        if (value is not null)
            _ = value.Page.OnNavigatedToAsync();
    }

    public void Receive(NavigateMessage message) =>
        SelectedItem = Pages.FirstOrDefault(p => p.Page.GetType() == message.PageType) ?? SelectedItem;

    /// <summary>
    /// Labels follow the culture through their bindings; numbers and dates formatted by StringFormat use the element
    /// Language, so the current page is rebuilt to pick up the new formats.
    /// </summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Loc.Culture))
            return;

        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(Title));

        var page = CurrentPage;
        CurrentPage = null;
        CurrentPage = page;
    }
}
