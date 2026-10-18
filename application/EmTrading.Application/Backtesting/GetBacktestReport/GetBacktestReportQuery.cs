using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Common;
using EmTrading.Domain.Backtesting;
using EmTrading.Domain.MarketData;
using FluentValidation;

namespace EmTrading.Application.Backtesting.GetBacktestReport;

/// <summary>
/// Report of one finished run. A run's result never changes, so it is cached for a long time.
/// </summary>
public sealed record GetBacktestReportQuery(string Symbol, string RunId) : IQuery<BacktestReport>, ICacheableQuery
{
    private string NormalizedSymbol => Symbol.Trim().ToUpperInvariant();

    public string CacheKey => BacktestCacheKeys.Report(NormalizedSymbol, RunId);

    public IReadOnlyCollection<string> CacheTags => [];

    public TimeSpan CacheDuration => TimeSpan.FromHours(1);
}

internal sealed class GetBacktestReportQueryValidator : AbstractValidator<GetBacktestReportQuery>
{
    public GetBacktestReportQueryValidator()
    {
        RuleFor(q => q.Symbol).MustBeSymbol();
        RuleFor(q => q.RunId).MustBeRunId();
    }
}

internal sealed class GetBacktestReportQueryHandler(IBacktestResultReader reader) : IQueryHandler<GetBacktestReportQuery, BacktestReport>
{
    public async Task<Result<BacktestReport>> HandleAsync(GetBacktestReportQuery query, CancellationToken cancellationToken)
    {
        var report = await reader.ReadAsync(Symbol.Create(query.Symbol), BacktestRunId.From(query.RunId), cancellationToken);
        return report is null ? BacktestErrors.ReportNotFound(query.RunId) : report;
    }
}
