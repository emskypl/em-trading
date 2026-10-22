using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Instruments.QueueInstrumentRefresh;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmTrading.Infrastructure.Instruments;

/// <summary>
/// At startup, queues a refresh of the instrument list when it is missing or out of date. The refresh itself runs as a
/// background job, so startup does not wait for the download.
/// </summary>
internal sealed class InstrumentRefreshScheduler(IDispatcher dispatcher, ILogger<InstrumentRefreshScheduler> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new QueueInstrumentRefreshCommand(OnlyIfStale: true), cancellationToken);
        if (result.IsFailure)
            logger.LogWarning("Could not schedule the instrument list refresh: {Error}", result.Error.Message);
        else if (result.Value.WasQueued)
            logger.LogInformation("Instrument list is missing or out of date; refresh queued as job {JobId}", result.Value.JobId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
