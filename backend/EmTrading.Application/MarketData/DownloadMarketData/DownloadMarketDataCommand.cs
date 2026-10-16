using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Common;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;
using FluentValidation;

namespace EmTrading.Application.MarketData.DownloadMarketData;

public sealed record DownloadMarketDataCommand(string Symbol, Timeframe Timeframe, DateOnly Start, DateOnly End)
    : ICommand<MarketDataDownloadResult>
{
    /// <summary>Optional live progress (0 to 100) for the caller; not part of the command's data.</summary>
    public IProgress<int>? Progress { get; init; }
}

internal sealed class DownloadMarketDataCommandValidator : AbstractValidator<DownloadMarketDataCommand>
{
    public DownloadMarketDataCommandValidator(TimeProvider clock)
    {
        RuleFor(c => c.Symbol).MustBeSymbol();
        RuleFor(c => c.Timeframe).IsInEnum();
        RuleFor(c => c.Start).MustNotBeInTheFuture(clock);
        RuleFor(c => c.End).GreaterThanOrEqualTo(c => c.Start).WithMessage("End date must not be before start date.");
    }
}

internal sealed class DownloadMarketDataCommandHandler(IMarketDataDownloader downloader)
    : ICommandHandler<DownloadMarketDataCommand, MarketDataDownloadResult>
{
    public async Task<Result<MarketDataDownloadResult>> HandleAsync(DownloadMarketDataCommand command, CancellationToken cancellationToken)
    {
        var request = new MarketDataRequest(
            Symbol.Create(command.Symbol),
            command.Timeframe,
            DateRange.Create(command.Start, command.End));

        var result = await downloader.DownloadAsync(request, command.Progress, cancellationToken);
        return result.BarCount == 0 ? MarketDataErrors.NoData(request) : result;
    }
}

public static class MarketDataErrors
{
    public static Error NoData(MarketDataRequest request) =>
        Error.NotFound("MarketData.NoData", $"No {request.Timeframe} data for {request.Symbol} in {request.Period}.");
}
