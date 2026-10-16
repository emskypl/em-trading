using EmTrading.Application.MarketData;
using EmTrading.Infrastructure.Configuration;
using EmTrading.Infrastructure.MarketData.Alpaca;
using EmTrading.Infrastructure.MarketData.Lean;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure.MarketData;

internal sealed class MarketDataDownloader(
    AlpacaMarketDataClient alpaca,
    LeanReferenceDataClient referenceData,
    LeanEquityDataWriter writer,
    IOptions<StorageOptions> storage,
    TimeProvider clock,
    ILogger<MarketDataDownloader> logger) : IMarketDataDownloader
{
    // Share of the progress bar given to each stage; fetching dominates, writing is quick.
    private const int ReferenceDataEnd = 5;
    private const int FetchEnd = 80;

    public async Task<MarketDataDownloadResult> DownloadAsync(
        MarketDataRequest request,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var symbol = request.Symbol.Value;

        progress?.Report(0);
        await referenceData.EnsureReferenceDataAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(ReferenceDataEnd);

        var startUtc = request.Period.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfLastDayUtc = request.Period.End.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var endUtc = endOfLastDayUtc < nowUtc ? endOfLastDayUtc : nowUtc;

        logger.LogInformation("Downloading {Timeframe} bars for {Symbol} in {Period}", request.Timeframe, symbol, request.Period);

        var bars = await alpaca.GetBarsAsync(
            symbol, request.Timeframe, startUtc, endUtc,
            Scale(progress, ReferenceDataEnd, FetchEnd),
            cancellationToken).ConfigureAwait(false);

        if (bars.Count == 0)
        {
            logger.LogInformation("No {Timeframe} bars for {Symbol} in {Period}", request.Timeframe, symbol, request.Period);
            return new MarketDataDownloadResult(symbol, 0, 0);
        }

        var files = await writer.WriteAsync(
            storage.Value.DataFolder, symbol, request.Timeframe, bars,
            Scale(progress, FetchEnd, 100),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Stored {BarCount} bars for {Symbol} in {FileCount} file(s)", bars.Count, symbol, files);
        progress?.Report(100);
        return new MarketDataDownloadResult(symbol, bars.Count, files);
    }

    /// <summary>
    /// Maps a 0..1 stage progress onto the [from, to] range of the overall percentage.
    /// </summary>
    private static IProgress<double>? Scale(IProgress<int>? progress, int from, int to) =>
        progress is null ? null : new ScaledProgress(progress, from, to);

    // Reports synchronously; Progress<T> would post each value separately and could deliver them out of order.
    private sealed class ScaledProgress(IProgress<int> inner, int from, int to) : IProgress<double>
    {
        public void Report(double value) => inner.Report(from + (int)Math.Round((to - from) * value));
    }
}
