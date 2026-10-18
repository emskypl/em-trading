using EmTrading.Domain.Backtesting;
using EmTrading.Domain.Common;

namespace EmTrading.Domain.Optimization;

public enum TrialStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// One point of the parameter grid: a backtest with a full set of parameters. Changed only through its
/// <see cref="OptimizationRun"/>; the id is the trial's position in the grid.
/// </summary>
public sealed class OptimizationTrial : Entity<int>
{
    internal OptimizationTrial(int index, StrategyParameters parameters) : base(index)
    {
        Parameters = parameters;
        Status = TrialStatus.Pending;
    }

#pragma warning disable CS8618 // Persistence materializes trials through this constructor and then sets every property.
    private OptimizationTrial() : base(0)
    {
    }
#pragma warning restore CS8618

    public int Index => Id;

    /// <summary>Every parameter of the run, fixed and varied, so the trial can be reproduced as a normal backtest.</summary>
    public StrategyParameters Parameters { get; }

    public TrialStatus Status { get; private set; }

    public BacktestRunId? RunId { get; private set; }

    public TrialMetrics? Metrics { get; private set; }

    public string? FailureReason { get; private set; }

    public bool IsFinished => Status is TrialStatus.Completed or TrialStatus.Failed or TrialStatus.Cancelled;

    internal void Start(BacktestRunId runId)
    {
        EnsureStatus(TrialStatus.Pending, "start");
        Status = TrialStatus.Running;
        RunId = runId;
    }

    internal void Complete(TrialMetrics metrics)
    {
        EnsureStatus(TrialStatus.Running, "complete");
        Status = TrialStatus.Completed;
        Metrics = metrics;
    }

    internal void Fail(string reason)
    {
        EnsureStatus(TrialStatus.Running, "fail");
        Status = TrialStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown error" : reason.Trim();
    }

    internal void Cancel()
    {
        if (!IsFinished)
            Status = TrialStatus.Cancelled;
    }

    private void EnsureStatus(TrialStatus expected, string action)
    {
        if (Status != expected)
            throw new DomainException($"Cannot {action} trial {Index}: it is {Status}.");
    }
}
