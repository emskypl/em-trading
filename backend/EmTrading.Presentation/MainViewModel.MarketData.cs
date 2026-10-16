using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Application.MarketData;
using EmTrading.Application.MarketData.GetMarketDataCoverage;
using EmTrading.Application.MarketData.QueueMarketDataDownloads;

namespace EmTrading.Presentation;

/// <summary>
/// Market data downloads run as background jobs; this part of the view model queues them and mirrors their state.
/// </summary>
public partial class MainViewModel
{
    // Job progress can change many times per second on worker threads; the grid is refreshed at most this often.
    private static readonly TimeSpan JobsRefreshInterval = TimeSpan.FromMilliseconds(250);

    private static readonly char[] SymbolSeparators = [',', ';', ' '];

    private int _jobsRefreshScheduled;
    private int _finishedJobCount;

    [ObservableProperty] private string _downloadSymbols = "AAPL";
    [ObservableProperty] private IReadOnlyList<JobSnapshot> _jobs = [];
    [ObservableProperty] private IReadOnlyList<MarketDataCoverage> _marketDataCoverage = [];

    /// <summary>Queues one job per symbol and returns at once; the jobs tab shows their progress.</summary>
    [RelayCommand]
    private async Task DownloadDataAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDateRange(out var start, out var end))
            return;

        var symbols = DownloadSymbols.Split(SymbolSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await _dispatcher.SendAsync(
            new QueueMarketDataDownloadsCommand(symbols, SelectedTimeframe, start, end), cancellationToken);

        StatusMessage = result.Match(
            queued => queued.AlreadyQueued.Count == 0
                ? $"Queued {queued.JobIds.Count} download job(s)."
                : $"Queued {queued.JobIds.Count} download job(s); already queued: {string.Join(", ", queued.AlreadyQueued)}.",
            Describe);
    }

    [RelayCommand]
    private void CancelJob(Guid jobId) => _jobQueue.TryCancel(jobId);

    [RelayCommand]
    private void ClearFinishedJobs() => _jobQueue.ClearFinished();

    [RelayCommand]
    private async Task RefreshCoverageAsync(CancellationToken cancellationToken)
    {
        var coverage = await _dispatcher.SendAsync(new GetMarketDataCoverageQuery(), cancellationToken);
        if (coverage.IsSuccess)
            MarketDataCoverage = coverage.Value;
        else
            StatusMessage = Describe(coverage.Error);
    }

    /// <summary>
    /// Raised on worker threads. Bursts of notifications collapse into one UI update per interval.
    /// </summary>
    private void OnJobsChanged(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _jobsRefreshScheduled, 1) == 1)
            return;

        _ = RefreshJobsAfterDelayAsync();
    }

    private async Task RefreshJobsAfterDelayAsync()
    {
        await Task.Delay(JobsRefreshInterval).ConfigureAwait(false);

        // Reset before taking the snapshot: a change arriving after this point schedules another refresh.
        Volatile.Write(ref _jobsRefreshScheduled, 0);
        var snapshot = _jobMonitor.GetJobs();

        OnUiThread(() => ApplyJobs(snapshot));
    }

    private void ApplyJobs(IReadOnlyList<JobSnapshot> snapshot)
    {
        Jobs = snapshot;

        // A finished download changes what is stored locally.
        var finished = snapshot.Count(j => j.Status == JobStatus.Succeeded);
        if (finished != _finishedJobCount)
        {
            _finishedJobCount = finished;
            RefreshCoverageCommand.Execute(null);
        }
    }

    private void OnUiThread(Action action)
    {
        if (_uiContext is null)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }
}
