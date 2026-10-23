using CommunityToolkit.Mvvm.Messaging;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Instruments;
using EmTrading.Application.MarketData;
using EmTrading.Application.MarketData.GetMarketDataCoverage;
using EmTrading.Presentation.Localization;
using EmTrading.Presentation.Shell;
using EmTrading.Wpf.Controls;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation.Instruments;

/// <summary>
/// Symbol suggestions for fields that work on stored data (backtests, optimization): only symbols whose market data
/// is downloaded, with the company name and which intervals and days are stored. Reloads when downloads finish.
/// </summary>
public sealed class DownloadedSymbolCatalog : ISymbolSuggestionProvider, IRecipient<MarketDataChangedMessage>, IRecipient<InstrumentsChangedMessage>
{
    private readonly IDispatcher _dispatcher;
    private readonly InstrumentCatalog _instruments;
    private readonly ILogger<DownloadedSymbolCatalog> _logger;
    private volatile Snapshot _snapshot = new([], new Dictionary<string, IReadOnlyList<MarketDataCoverage>>());

    public DownloadedSymbolCatalog(IDispatcher dispatcher, IMessenger messenger, InstrumentCatalog instruments, ILogger<DownloadedSymbolCatalog> logger)
    {
        _dispatcher = dispatcher;
        _instruments = instruments;
        _logger = logger;
        messenger.RegisterAll(this);
    }

    public int Count => _snapshot.Symbols.Count;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var coverage = await _dispatcher.SendAsync(new GetMarketDataCoverageQuery(), cancellationToken);
        if (coverage.IsFailure)
        {
            _logger.LogWarning("Could not load the stored market data: {Error}", coverage.Error.Message);
            return;
        }

        var bySymbol = coverage.Value
            .Where(c => c.Ranges.Count > 0)
            .GroupBy(c => c.Symbol)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MarketDataCoverage>)g.OrderBy(c => c.Timeframe).ToList());

        // Searched like the full list: the same ranking by symbol and company name.
        var symbols = bySymbol.Keys
            .Order(StringComparer.Ordinal)
            .Select(symbol => new InstrumentInfo(symbol, _instruments.Find(symbol)?.Name ?? string.Empty, string.Empty))
            .ToList();

        _snapshot = new Snapshot(symbols, bySymbol);
    }

    public async void Receive(MarketDataChangedMessage message) => await LoadAsync();

    /// <summary>The instrument list arrived later than the stored data: reload to pick up company names.</summary>
    public async void Receive(InstrumentsChangedMessage message) => await LoadAsync();

    public IReadOnlyList<SymbolSuggestion> Suggest(string text, int maxResults)
    {
        var snapshot = _snapshot;
        var matches = string.IsNullOrWhiteSpace(text)
            ? snapshot.Symbols.Take(maxResults)
            : InstrumentCatalog.Search(snapshot.Symbols, text, maxResults);

        return matches.Select(i => new SymbolSuggestion(i.Symbol, i.Name, Describe(snapshot.Coverage[i.Symbol]))).ToList();
    }

    /// <summary>e.g. "Minutowy 2023-10-20..2026-10-19, Dzienny 2025-10-16..2026-10-15", in the current language.</summary>
    internal static string Describe(IReadOnlyList<MarketDataCoverage> coverage) =>
        string.Join(", ", coverage.Select(c =>
            $"{(Loc.TryGet($"Timeframe_{c.Timeframe}", out var name) ? name : c.Timeframe.ToString())} {c.Ranges[0].Start:yyyy-MM-dd}..{c.Ranges[^1].End:yyyy-MM-dd}"));

    private sealed record Snapshot(IReadOnlyList<InstrumentInfo> Symbols, IReadOnlyDictionary<string, IReadOnlyList<MarketDataCoverage>> Coverage);
}
