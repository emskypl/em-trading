using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Backtesting;
using EmTrading.Application.MarketData;
using EmTrading.Domain.MarketData;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation;

public partial class MainViewModel : ObservableObject
{
    private const string DefaultStrategy = "SmaCrossStrategy";

    private readonly IMarketDataDownloader _marketDataDownloader;
    private readonly IBacktestEngine _backtestEngine;
    private readonly IBacktestResultReader _resultReader;
    private readonly ILogger<MainViewModel> _logger;

    // Progress<T> posts to the UI thread asynchronously, so a report can arrive after its operation ended.
    // Reports are only applied while their operation is the current one.
    private int _operationId;

    public MainViewModel(
        IMarketDataDownloader marketDataDownloader,
        IBacktestEngine backtestEngine,
        IBacktestResultReader resultReader,
        ILogger<MainViewModel> logger)
    {
        _marketDataDownloader = marketDataDownloader;
        _backtestEngine = backtestEngine;
        _resultReader = resultReader;
        _logger = logger;
    }

    public string Title => "EmTrading";

    // Inputs
    public IReadOnlyList<string> Symbols { get; } = ["AAPL", "AMZN", "MMM", "MSFT"];
    public IReadOnlyList<Timeframe> Timeframes { get; } = Enum.GetValues<Timeframe>();
    public IReadOnlyList<string> Strategies { get; } = [DefaultStrategy];

    [ObservableProperty] private string _symbol = "AAPL";
    [ObservableProperty] private Timeframe _selectedTimeframe = Timeframe.Minute;
    [ObservableProperty] private DateTime? _startDate = DateTime.Today.AddYears(-1);
    [ObservableProperty] private DateTime? _endDate = DateTime.Today;
    [ObservableProperty] private string _selectedStrategy = DefaultStrategy;
    [ObservableProperty] private int _fastPeriod = 26;
    [ObservableProperty] private int _slowPeriod = 130;
    [ObservableProperty] private decimal _initialCapital = 1_000_000m;

    // Status
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadDataCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunBacktestCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadLatestResultsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty] private int _currentProgress;
    [ObservableProperty] private string _statusMessage = "Ready";

    // Backtest results
    [ObservableProperty] private string _compoundingReturn = "0%";
    [ObservableProperty] private string _maxDrawdown = "0%";
    [ObservableProperty] private string _winRate = "0%";
    [ObservableProperty] private string _profitFactor = "0";
    [ObservableProperty] private string _sharpeRatio = "0";
    [ObservableProperty] private string _totalTrades = "0";
    [ObservableProperty] private string _averageWin = "0%";
    [ObservableProperty] private string _averageLoss = "0%";
    [ObservableProperty] private int _maxConsecutiveWins;
    [ObservableProperty] private int _maxConsecutiveLosses;
    [ObservableProperty] private IReadOnlyList<ClosedTrade> _tradesHistory = [];

    // LiveCharts
    [ObservableProperty] private SeriesCollection _equitySeriesCollection = new();
    [ObservableProperty] private IList<string> _chartLabels = [];
    public Func<double, string> EquityFormatter { get; } = value => value.ToString("N0", CultureInfo.CurrentCulture);

    private bool CanStartOperation() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task DownloadDataAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDateRange(out var start, out var end))
            return;

        var request = new MarketDataRequest(Symbol, SelectedTimeframe, start, end);
        await RunOperationAsync($"Downloading {SelectedTimeframe} data for {Symbol}...", async operationId =>
        {
            var progress = CreateProgress<int>(operationId, percent => CurrentProgress = percent);
            var result = await _marketDataDownloader.DownloadAsync(request, progress, cancellationToken);
            return $"Downloaded {result.BarCount:N0} bars for {result.Symbol}.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task RunBacktestAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDateRange(out var start, out var end))
            return;

        if (FastPeriod <= 0 || SlowPeriod <= FastPeriod)
        {
            StatusMessage = "Slow period must be greater than fast period, and both must be positive.";
            return;
        }

        var parameters = new Dictionary<string, string>
        {
            [StrategyParameterNames.FastPeriod] = FastPeriod.ToString(CultureInfo.InvariantCulture),
            [StrategyParameterNames.SlowPeriod] = SlowPeriod.ToString(CultureInfo.InvariantCulture)
        };
        var request = new BacktestRequest(SelectedStrategy, Symbol, start, end, InitialCapital, parameters);

        await RunOperationAsync($"Running {SelectedStrategy} backtest for {Symbol}...", async operationId =>
        {
            var progress = CreateProgress<BacktestProgress>(operationId, update =>
            {
                CurrentProgress = update.Percent;
                if (update.Message is { Length: > 0 } message)
                    StatusMessage = message;
            });

            var run = await _backtestEngine.RunAsync(request, progress, cancellationToken);
            ShowReport(await _resultReader.ReadAsync(run, cancellationToken));
            return $"Backtest {run.RunId} completed.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task LoadLatestResultsAsync(CancellationToken cancellationToken)
    {
        await RunOperationAsync($"Loading latest results for {Symbol}...", async _ =>
        {
            var report = await _resultReader.ReadLatestAsync(Symbol, SelectedStrategy, cancellationToken);
            if (report is null)
                return $"No saved results for {Symbol} / {SelectedStrategy}.";

            ShowReport(report);
            return $"Loaded {report.RunId}.";
        });
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        DownloadDataCommand.Cancel();
        RunBacktestCommand.Cancel();
        LoadLatestResultsCommand.Cancel();
    }

    /// <param name="operation">Receives the operation id and returns the final status message.</param>
    private async Task RunOperationAsync(string startMessage, Func<int, Task<string>> operation)
    {
        var operationId = ++_operationId;
        IsBusy = true;
        CurrentProgress = 0;
        StatusMessage = startMessage;

        try
        {
            StatusMessage = await operation(operationId);
            CurrentProgress = 100;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed: {Operation}", startMessage);
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Progress<T> CreateProgress<T>(int operationId, Action<T> apply) =>
        new(value =>
        {
            if (IsBusy && operationId == _operationId)
                apply(value);
        });

    private bool TryGetDateRange(out DateOnly start, out DateOnly end)
    {
        start = end = default;
        if (StartDate is null || EndDate is null || StartDate > EndDate)
        {
            StatusMessage = "Select a valid date range.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            StatusMessage = "Enter a symbol.";
            return false;
        }

        start = DateOnly.FromDateTime(StartDate.Value);
        end = DateOnly.FromDateTime(EndDate.Value);
        return true;
    }

    private void ShowReport(BacktestReport report)
    {
        string Stat(string name, string fallback) => report.Statistics.GetValueOrDefault(name, fallback);

        CompoundingReturn = Stat("Compounding Annual Return", "0%");
        MaxDrawdown = Stat("Drawdown", "0%");
        WinRate = Stat("Win Rate", "0%");
        SharpeRatio = Stat("Sharpe Ratio", "0");
        AverageWin = Stat("Average Win", "0%");
        AverageLoss = Stat("Average Loss", "0%");
        ProfitFactor = report.TradeStatistics.ProfitFactor.ToString("0.00", CultureInfo.CurrentCulture);
        TotalTrades = report.TradeStatistics.TotalTrades.ToString(CultureInfo.CurrentCulture);
        MaxConsecutiveWins = report.TradeStatistics.MaxConsecutiveWins;
        MaxConsecutiveLosses = report.TradeStatistics.MaxConsecutiveLosses;
        TradesHistory = report.ClosedTrades;

        EquitySeriesCollection = new SeriesCollection
        {
            new LineSeries
            {
                Title = "Kapitał ($)",
                Values = new ChartValues<double>(report.Equity.Select(p => (double)p.Value)),
                PointGeometry = null,
                StrokeThickness = 2
            }
        };
        ChartLabels = report.Equity.Select(p => p.Time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
    }
}
