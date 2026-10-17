using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Backtesting.Strategies;
using EmTrading.Domain.Backtesting;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;

namespace EmTrading.Application.Backtesting.RunBacktest;

/// <summary>
/// Saves the run as Running before the engine starts, so it shows up in the history straight away,
/// and saves its final state on every outcome: completed, failed, cancelled or crashed.
/// </summary>
internal sealed class RunBacktestCommandHandler(
    IBacktestEngine engine,
    IBacktestRunRepository repository,
    IUnitOfWork unitOfWork,
    IStrategyCatalog strategies,
    TimeProvider clock) : ICommandHandler<RunBacktestCommand, BacktestRunResult>
{
    public async Task<Result<BacktestRunResult>> HandleAsync(RunBacktestCommand command, CancellationToken cancellationToken)
    {
        var symbol = Symbol.Create(command.Symbol);
        var strategy = StrategyName.Create(command.StrategyName);

        // The validator has checked the strategy exists; storing every value (defaults included)
        // keeps the run reproducible even if a default changes later.
        var parameters = strategies.Find(strategy.Value)!.WithDefaults(command.Parameters);

        var run = BacktestRun.Create(
            BacktestRunId.New(symbol, strategy, clock.GetLocalNow()),
            symbol,
            strategy,
            DateRange.Create(command.Start, command.End),
            Money.Usd(command.InitialCapital),
            StrategyParameters.Create(parameters),
            clock.GetUtcNow());

        run.Start(clock.GetUtcNow());
        repository.Add(run);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // From here on the run exists in storage, so every outcome is saved, even if the caller has cancelled.
        try
        {
            await engine.RunAsync(BacktestRequest.For(run), new RunProgress(run, command.Progress), cancellationToken);
            run.Complete(clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Cancel(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (BacktestEngineException ex)
        {
            run.Fail(ex.Message, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return BacktestErrors.EngineFailed(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unexpected: record the failure so the run never stays "Running", then let the exception surface.
            run.Fail(ex.Message, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        return new BacktestRunResult(run.Id.Value, symbol.Value, strategy.Value);
    }

    /// <summary>
    /// Records progress on the aggregate and forwards it to the caller.
    /// The engine reports from one output thread and finishes reporting before RunAsync returns,
    /// and nothing is saved while it runs, so the aggregate is never touched by two threads at once.
    /// </summary>
    private sealed class RunProgress(BacktestRun run, IProgress<BacktestProgress>? caller) : IProgress<BacktestProgress>
    {
        public void Report(BacktestProgress value)
        {
            run.ReportProgress(value.Percent);
            caller?.Report(value);
        }
    }
}
