using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.GetBacktestHistory;
using EmTrading.Application.Backtesting.GetBacktestReport;
using EmTrading.Application.Backtesting.GetLatestBacktestReport;
using EmTrading.Application.Backtesting.RunBacktest;
using EmTrading.Domain.MarketData;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly IDispatcher _dispatcher;
    private readonly IJobQueue _jobQueue;
    private readonly IJobMonitor _jobMonitor;
    private readonly ILogger<MainViewModel> _logger;

    // Captured on the UI thread; background notifications are posted back through it.
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    // Progress<T> posts to the UI thread asynchronously, so a report can arrive after its operation ended.
    // Reports are only applied while their operation is the current one.
    private int _operationId;

    public MainViewModel(IDispatcher dispatcher, IJobQueue jobQueue, IJobMonitor jobMonitor, ILogger<MainViewModel> logger)
    {
        _dispatcher = dispatcher;
        _jobQueue = jobQueue;
        _jobMonitor = jobMonitor;
        _logger = logger;
        _jobMonitor.JobsChanged += OnJobsChanged;
    }

    public string Title => "EmTrading";

    // Inputs
    public IReadOnlyList<string> Symbols { get; } = ["AAPL", "AMZN", "MMM", "MSFT"];
    public IReadOnlyList<Timeframe> Timeframes { get; } = Enum.GetValues<Timeframe>();

    [ObservableProperty] private string _symbol = "AAPL";
    [ObservableProperty] private Timeframe _selectedTimeframe = Timeframe.Minute;
    [ObservableProperty] private DateTime? _startDate = DateTime.Today.AddYears(-1);
    [ObservableProperty] private DateTime? _endDate = DateTime.Today;
    [ObservableProperty] private decimal _initialCapital = 1_000_000m;

    // Status
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunBacktestCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadLatestResultsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenHistoryRunCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareRunsCommand))]
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

    // Backtest history
    [ObservableProperty] private IReadOnlyList<BacktestRunSummary> _backtestHistory = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenHistoryRunCommand))]
    private BacktestRunSummary? _selectedHistoryRun;

    // LiveCharts
    [ObservableProperty] private SeriesCollection _equitySeriesCollection = new();
    [ObservableProperty] private IList<string> _chartLabels = [];
    public Func<double, string> EquityFormatter { get; } = value => value.ToString("N0", CultureInfo.CurrentCulture);

    private bool CanStartOperation() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task RunBacktestAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDateRange(out var start, out var end) || !TryGetStrategy(out var strategy, out var parameters))
            return;

        await RunOperationAsync($"Running {strategy.DisplayName} backtest for {Symbol}...", async operationId =>
        {
            var command = new RunBacktestCommand(strategy.Name, Symbol, start, end, InitialCapital, parameters)
            {
                Progress = CreateProgress<BacktestProgress>(operationId, update =>
                {
                    CurrentProgress = update.Percent;
                    if (update.Message is { Length: > 0 } message)
                        StatusMessage = message;
                })
            };

            var run = await _dispatcher.SendAsync(command, cancellationToken);
            if (run.IsFailure)
                return Describe(run.Error);

            var report = await _dispatcher.SendAsync(new GetBacktestReportQuery(run.Value.Symbol, run.Value.RunId), cancellationToken);
            if (report.IsFailure)
                return Describe(report.Error);

            ShowReport(report.Value);
            return $"Backtest {run.Value.RunId} completed.";
        });

        // Every outcome (completed, failed, cancelled) is stored, so the history changes either way.
        await RefreshHistoryAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task LoadLatestResultsAsync(CancellationToken cancellationToken)
    {
        if (SelectedStrategy is not { } strategy)
            return;

        await RunOperationAsync($"Loading latest results for {Symbol}...", async _ =>
        {
            var report = await _dispatcher.SendAsync(new GetLatestBacktestReportQuery(Symbol, strategy.Name), cancellationToken);
            if (report.IsFailure)
                return Describe(report.Error);

            ShowReport(report.Value);
            return $"Loaded {report.Value.RunId}.";
        });
    }

    [RelayCommand]
    private async Task RefreshHistoryAsync(CancellationToken cancellationToken)
    {
        var history = await _dispatcher.SendAsync(new GetBacktestHistoryQuery(), cancellationToken);
        if (history.IsFailure)
        {
            StatusMessage = Describe(history.Error);
            return;
        }

        var selectedRunId = SelectedHistoryRun?.RunId;
        BacktestHistory = history.Value;
        SelectedHistoryRun = BacktestHistory.FirstOrDefault(r => r.RunId == selectedRunId);
    }

    private bool CanOpenHistoryRun() => !IsBusy && SelectedHistoryRun is { HasReport: true };

    [RelayCommand(CanExecute = nameof(CanOpenHistoryRun))]
    private async Task OpenHistoryRunAsync(CancellationToken cancellationToken)
    {
        var run = SelectedHistoryRun!;
        await RunOperationAsync($"Loading results of {run.RunId}...", async _ =>
        {
            var report = await _dispatcher.SendAsync(new GetBacktestReportQuery(run.Symbol, run.RunId), cancellationToken);
            if (report.IsFailure)
                return Describe(report.Error);

            ShowReport(report.Value);
            return $"Loaded {run.RunId}.";
        });
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        RunBacktestCommand.Cancel();
        LoadLatestResultsCommand.Cancel();
        OpenHistoryRunCommand.Cancel();
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

    /// <summary>
    /// Only checks that dates are picked; the rules themselves are enforced by the command validators.
    /// </summary>
    private bool TryGetDateRange(out DateOnly start, out DateOnly end)
    {
        start = end = default;
        if (StartDate is null || EndDate is null)
        {
            StatusMessage = "Select start and end dates.";
            return false;
        }

        start = DateOnly.FromDateTime(StartDate.Value);
        end = DateOnly.FromDateTime(EndDate.Value);
        return true;
    }

    private static string Describe(Error error) =>
        error.Type == ErrorType.Validation ? error.Message : $"Failed: {error.Message}";

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
