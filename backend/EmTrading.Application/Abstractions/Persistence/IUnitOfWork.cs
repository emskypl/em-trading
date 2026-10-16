namespace EmTrading.Application.Abstractions.Persistence;

/// <summary>
/// Commits every change made in the current scope as one transaction, then publishes the domain events
/// raised by the saved aggregates. Events are published only after the commit succeeds, so their handlers
/// always see the persisted state.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
