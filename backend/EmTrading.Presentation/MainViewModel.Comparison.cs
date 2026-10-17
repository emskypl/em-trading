using System.Collections;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.CompareBacktests;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;

namespace EmTrading.Presentation;

/// <summary>
/// Side-by-side comparison of runs selected in the history tab.
/// </summary>
public partial class MainViewModel
{
    public const int ComparisonTabIndex = 4;

    // X is days since year 1, so runs over different periods share one date axis.
    private static readonly CartesianMapper<DateTimePoint> DayMapper = Mappers.Xy<DateTimePoint>()
        .X(point => point.DateTime.Ticks / (double)TimeSpan.TicksPerDay)
        .Y(point => point.Value);

    [ObservableProperty] private IReadOnlyList<ComparedRun> _comparedRuns = [];
    [ObservableProperty] private SeriesCollection _comparisonSeries = new();
    [ObservableProperty] private int _selectedTabIndex;

    public Func<double, string> DayAxisFormatter { get; } =
        days => new DateTime((long)(days * TimeSpan.TicksPerDay)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public Func<double, string> PercentFormatter { get; } = value => value.ToString("0.#", CultureInfo.CurrentCulture) + "%";

    /// <param name="selectedRows">The history grid's selected items.</param>
    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task CompareRunsAsync(IList? selectedRows, CancellationToken cancellationToken)
    {
        var runs = (selectedRows ?? Array.Empty<object>()).OfType<BacktestRunSummary>()
            .Where(r => r.HasReport)
            .Select(r => new BacktestRunReference(r.Symbol, r.RunId))
            .ToList();

        await RunOperationAsync($"Comparing {runs.Count} run(s)...", async _ =>
        {
            var comparison = await _dispatcher.SendAsync(new CompareBacktestsQuery(runs), cancellationToken);
            if (comparison.IsFailure)
                return Describe(comparison.Error);

            ShowComparison(comparison.Value);
            SelectedTabIndex = ComparisonTabIndex;
            return $"Compared {runs.Count} runs.";
        });
    }

    private void ShowComparison(BacktestComparison comparison)
    {
        ComparedRuns = comparison.Runs;

        var series = new SeriesCollection(DayMapper);
        foreach (var run in comparison.Runs)
        {
            series.Add(new LineSeries
            {
                Title = $"{run.Symbol} · {run.RunId[..Math.Min(15, run.RunId.Length)]}",
                Values = new ChartValues<DateTimePoint>(run.CumulativeReturn.Select(p => new DateTimePoint(p.Time, (double)p.ReturnPercent))),
                PointGeometry = null,
                Fill = Brushes.Transparent,
                StrokeThickness = 2
            });
        }

        ComparisonSeries = series;
    }
}
