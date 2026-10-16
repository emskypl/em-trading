namespace EmTrading.Infrastructure.MarketData;

internal readonly record struct MarketBar(
    DateTime TimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
