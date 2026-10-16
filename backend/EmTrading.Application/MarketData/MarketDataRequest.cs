using EmTrading.Domain.MarketData;

namespace EmTrading.Application.MarketData;

public sealed record MarketDataRequest(string Symbol, Timeframe Timeframe, DateOnly Start, DateOnly End);

public sealed record MarketDataDownloadResult(string Symbol, int BarCount, int FilesWritten);
