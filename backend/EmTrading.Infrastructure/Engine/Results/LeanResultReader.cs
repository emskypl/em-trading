using System.Globalization;
using System.Text.Json;
using EmTrading.Application.Backtesting;
using EmTrading.Domain.Trading;
using EmTrading.Infrastructure.Configuration;
using EmTrading.Infrastructure.MarketData;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure.Engine.Results;

internal sealed class LeanResultReader(IOptions<StorageOptions> storage) : IBacktestResultReader
{
    private const string EquityChart = "Strategy Equity";
    private const string EquitySeries = "Equity";

    // Web defaults: camelCase names and numbers stored as strings (Lean writes e.g. "profitFactor": "1.3492").
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<BacktestReport> ReadAsync(BacktestRunResult run, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(storage.Value.ResultsFolder, run.Symbol, $"{run.RunId}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Result file for run {run.RunId} was not found.", path);

        return ReadFileAsync(path, cancellationToken);
    }

    public async Task<BacktestReport?> ReadLatestAsync(string symbol, string strategyName, CancellationToken cancellationToken = default)
    {
        var path = FindLatestResultFile(storage.Value.ResultsFolder, symbol.Trim().ToUpperInvariant(), strategyName);
        return path is null ? null : await ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Result files are named <c>{timestamp}_{SYMBOL}_{Strategy}.json</c>. Lean writes sibling files
    /// (<c>-summary</c>, <c>-order-events</c>, <c>-log</c>) and the app writes <c>-job</c>; those are skipped.
    /// </summary>
    internal static string? FindLatestResultFile(string resultsFolder, string symbol, string strategyName)
    {
        var folder = Path.Combine(resultsFolder, symbol);
        if (!Directory.Exists(folder))
            return null;

        var suffix = $"_{symbol}_{strategyName}.json";
        return new DirectoryInfo(folder)
            .EnumerateFiles($"*{suffix}")
            .Where(f => f.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    internal static async Task<BacktestReport> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        LeanResultDocument document;
        await using (var stream = File.OpenRead(path))
        {
            document = await JsonSerializer.DeserializeAsync<LeanResultDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidDataException($"Result file '{path}' is empty.");
        }

        var runId = Path.GetFileNameWithoutExtension(path);
        var tradeStats = document.TotalPerformance?.TradeStatistics;

        return new BacktestReport(
            runId,
            document.Statistics ?? new Dictionary<string, string>(),
            new TradeStatistics(
                tradeStats?.TotalNumberOfTrades ?? 0,
                tradeStats?.ProfitFactor ?? 0,
                tradeStats?.MaxConsecutiveWinningTrades ?? 0,
                tradeStats?.MaxConsecutiveLosingTrades ?? 0),
            ReadEquity(document),
            ReadClosedTrades(document));
    }

    private static List<EquityPoint> ReadEquity(LeanResultDocument document)
    {
        if (document.Charts?.GetValueOrDefault(EquityChart)?.Series?.GetValueOrDefault(EquitySeries)?.Values is not { } values)
            return [];

        // Last element is the value for line series and the close for candlestick series.
        return values
            .Where(v => v.Count >= 2 && v[0].HasValue && v[^1].HasValue)
            .Select(v => new EquityPoint(
                ExchangeTime.FromUtc(DateTime.UnixEpoch.AddSeconds(v[0]!.Value)),
                (decimal)v[^1]!.Value))
            .ToList();
    }

    private static List<ClosedTrade> ReadClosedTrades(LeanResultDocument document)
    {
        var trades = document.TotalPerformance?.ClosedTrades ?? [];
        return trades
            .Select((t, index) => new ClosedTrade(
                index + 1,
                t.Symbols?.FirstOrDefault()?.Value ?? string.Empty,
                t.Direction == 1 ? TradeDirection.Short : TradeDirection.Long,
                t.Quantity,
                ExchangeTime.FromUtc(t.EntryTime.ToUniversalTime()),
                t.EntryPrice,
                ExchangeTime.FromUtc(t.ExitTime.ToUniversalTime()),
                t.ExitPrice,
                ExitReason(document, t),
                t.ProfitLoss,
                t.TotalFees))
            .ToList();
    }

    private static string ExitReason(LeanResultDocument document, LeanClosedTrade trade)
    {
        if (trade.OrderIds is not { Count: > 0 } orderIds || document.Orders is null)
            return string.Empty;

        return document.Orders.GetValueOrDefault(orderIds[^1].ToString(CultureInfo.InvariantCulture))?.Tag ?? string.Empty;
    }
}
