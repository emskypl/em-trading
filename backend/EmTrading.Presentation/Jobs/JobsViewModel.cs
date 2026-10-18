using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Presentation.Common;
using EmTrading.Presentation.Shell;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation.Jobs;

/// <summary>
/// Background jobs. Mirrors <see cref="IJobMonitor"/>, whose notifications arrive on worker threads many times per
/// second, through a <see cref="ThrottledAction"/>; tells other pages when jobs that change their data finish.
/// </summary>
public sealed partial class JobsViewModel : PageViewModel
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly IJobQueue _queue;
    private readonly IJobMonitor _monitor;
    private readonly IMessenger _messenger;
    private readonly ThrottledAction _refresh;
    private readonly HashSet<Guid> _seenFinished = [];

    public JobsViewModel(IJobQueue queue, IJobMonitor monitor, IMessenger messenger, StatusViewModel status, ILogger<JobsViewModel> logger)
        : base(status, logger)
    {
        _queue = queue;
        _monitor = monitor;
        _messenger = messenger;
        _refresh = new ThrottledAction(RefreshInterval, Apply, SynchronizationContext.Current);
        _monitor.JobsChanged += (_, _) => _refresh.Request();
    }

    [ObservableProperty] private IReadOnlyList<JobSnapshot> _jobs = [];
    [ObservableProperty] private int _activeCount;

    [RelayCommand]
    private void Cancel(Guid jobId) => _queue.TryCancel(jobId);

    [RelayCommand]
    private void ClearFinished() => _queue.ClearFinished();

    private void Apply()
    {
        Jobs = _monitor.GetJobs();
        ActiveCount = Jobs.Count(j => j.IsActive);

        var newlyFinished = Jobs.Where(j => !j.IsActive && _seenFinished.Add(j.Id)).ToList();
        if (newlyFinished.Any(j => j.Key?.StartsWith("backtest:", StringComparison.Ordinal) == true))
            _messenger.Send(new BacktestRunsChangedMessage());
        if (newlyFinished.Any(j => j.Key?.StartsWith("backtest:", StringComparison.Ordinal) != true && j.Status == JobStatus.Succeeded))
            _messenger.Send(new MarketDataChangedMessage());
    }
}
