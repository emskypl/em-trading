namespace EmTrading.Wpf.Controls.Charts;

public readonly record struct TimeValue(DateTime Time, double Value);

/// <param name="Span">Time covered by the bar, e.g. one day.</param>
public readonly record struct PriceBar(DateTime Time, TimeSpan Span, double Open, double High, double Low, double Close);

/// <summary>One round trip drawn on a price chart: entry marker, exit marker and a line between them.</summary>
public readonly record struct TradeMarker(DateTime EntryTime, double EntryPrice, DateTime ExitTime, double ExitPrice, bool IsLong, bool IsWin);

public sealed record ChartSeries(string Label, IReadOnlyList<TimeValue> Points);
