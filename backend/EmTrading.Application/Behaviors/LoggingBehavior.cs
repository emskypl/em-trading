using System.Diagnostics;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Results;
using Microsoft.Extensions.Logging;

namespace EmTrading.Application.Behaviors;

/// <summary>
/// Outermost behavior: logs every request with its outcome and duration.
/// </summary>
internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();

        try
        {
            var result = await next().ConfigureAwait(false);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (result.IsSuccess)
                logger.LogInformation("{Request} succeeded in {ElapsedMs:0} ms", requestName, elapsedMs);
            else
                logger.LogWarning("{Request} failed in {ElapsedMs:0} ms: {ErrorCode} {ErrorMessage}",
                    requestName, elapsedMs, result.Error.Code, result.Error.Message);

            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{Request} cancelled after {ElapsedMs:0} ms", requestName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Request} threw after {ElapsedMs:0} ms", requestName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
