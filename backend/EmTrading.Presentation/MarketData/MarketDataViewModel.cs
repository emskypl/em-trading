using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.MarketData;
using EmTrading.Application.MarketData.GetMarketDataCoverage;
using EmTrading.Application.MarketData.GetPriceBars;
using EmTrading.Application.MarketData.QueueMarketDataDownloads;
using EmTrading.Domain.MarketData;
using EmTrading.Presentation.Localization;
using EmTrading.Presentation.Settings;
using EmTrading.Presentation.Shell;
using EmTrading.Wpf.Controls.Charts;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation.MarketData;

/// <summary>
/// Download data for a symbol list (background jobs), see what is stored, and preview it as a candlestick chart.
/// </summary>
public sealed partial class MarketDataViewModel : PageViewModel, IRecipient<MarketDataChangedMessage>
{
    private static readonly char[] SymbolSeparators = [',', ';', ' '];

    private readonly IDispatcher _dispatcher;

    public MarketDataViewModel(IDispatcher dispatcher, IMessenger messenger, StatusViewModel status, UserSettings settings, ILogger<MarketDataViewModel> logger)
        : base(status, logger)
    {
        _dispatcher = dispatcher;
        _symbolList = settings.DefaultSymbols;
        messenger.RegisterAll(this);
    }

    public IReadOnlyList<Timeframe> Timeframes { get; } = Enum.GetValues<Timeframe>();

    [ObservableProperty] private string _symbolList;
    [ObservableProperty] private Timeframe _selectedTimeframe = Timeframe.Minute;
    [ObservableProperty] private DateTime? _startDate = DateTime.Today.AddYears(-1);
    [ObservableProperty] private DateTime? _endDate = DateTime.Today;

    [ObservableProperty] private IReadOnlyList<MarketDataCoverage> _coverage = [];
    [ObservableProperty] private MarketDataCoverage? _selectedCoverage;
    [ObservableProperty] private IReadOnlyList<PriceBar> _previewBars = [];
    [ObservableProperty] private string? _previewTitle;

    protected override Task LoadAsync() => RefreshCoverageAsync(CancellationToken.None);

    public async void Receive(MarketDataChangedMessage message)
    {
        await RefreshCoverageAsync(CancellationToken.None);
        await LoadPreviewAsync(SelectedCoverage);
    }

    /// <summary>Queues one job per symbol and returns at once; the jobs page shows their progress.</summary>
    [RelayCommand]
    private async Task DownloadAsync(CancellationToken cancellationToken)
    {
        if (StartDate is null || EndDate is null)
        {
            Status.Show(Loc.Get("Status_SelectDates"));
            return;
        }

        var symbols = SymbolList.Split(SymbolSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await _dispatcher.SendAsync(new QueueMarketDataDownloadsCommand(
            symbols, SelectedTimeframe, DateOnly.FromDateTime(StartDate.Value), DateOnly.FromDateTime(EndDate.Value)), cancellationToken);

        if (result.IsFailure)
            Status.Show(result.Error);
        else if (result.Value.AlreadyQueued.Count == 0)
            Status.Show(Loc.Format("Status_QueuedDownloads", result.Value.JobIds.Count));
        else
            Status.Show(Loc.Format("Status_QueuedDownloadsSkipped", result.Value.JobIds.Count, string.Join(", ", result.Value.AlreadyQueued)));
    }

    [RelayCommand]
    private async Task RefreshCoverageAsync(CancellationToken cancellationToken)
    {
        var coverage = await _dispatcher.SendAsync(new GetMarketDataCoverageQuery(), cancellationToken);
        if (coverage.IsFailure)
        {
            Status.Show(coverage.Error);
            return;
        }

        var selected = SelectedCoverage;
        Coverage = coverage.Value;
        SelectedCoverage = Coverage.FirstOrDefault(c => c.Symbol == selected?.Symbol && c.Timeframe == selected.Timeframe);
    }

    partial void OnSelectedCoverageChanged(MarketDataCoverage? value) => _ = LoadPreviewAsync(value);

    /// <summary>Daily candles for daily data, hourly candles otherwise (minute data is aggregated).</summary>
    private async Task LoadPreviewAsync(MarketDataCoverage? coverage)
    {
        if (coverage is null || coverage.Ranges.Count == 0)
        {
            PreviewBars = [];
            PreviewTitle = null;
            return;
        }

        var resolution = coverage.Timeframe == Timeframe.Daily ? BarResolution.Daily : BarResolution.Hourly;
        var span = resolution == BarResolution.Daily ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1);
        var result = await _dispatcher.SendAsync(new GetPriceBarsQuery(coverage.Symbol, resolution, coverage.Ranges[0].Start, coverage.Ranges[^1].End));
        if (result.IsFailure)
        {
            Status.Show(result.Error);
            return;
        }

        PreviewBars = result.Value.Bars.Select(b => new PriceBar(b.Time, span, (double)b.Open, (double)b.High, (double)b.Low, (double)b.Close)).ToList();
        PreviewTitle = Loc.Format("MarketData_PreviewTitle", coverage.Symbol, Loc.Get($"Resolution_{resolution}"), PreviewBars.Count);
    }
}
