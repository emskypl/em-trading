namespace EmTrading.Infrastructure.MarketData;

/// <summary>
/// US equity data is stored by Lean in exchange time (New York).
/// </summary>
public static class ExchangeTime
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NewYork);
}
