using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace EmTrading.Infrastructure.Services;

public class DataDownloaderService
{
    private readonly HttpClient _httpClient;
    private readonly string _dataFolderPath;
    private readonly string _apiKey;
    private readonly string _apiSecret;

    /// <summary>
    /// Inicjalizuje serwis pobierania danych z Alpaca API.
    /// Wprowadź swoje klucze API ze strony Alpaca (dostępne za darmo po darmowej rejestracji).
    /// </summary>
    public DataDownloaderService(
        string apiKey = "TWOJ_ALPACA_API_KEY", 
        string apiSecret = "TWOJ_ALPACA_API_SECRET", 
        string dataFolderPath = "C:\\TradingData")
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _dataFolderPath = dataFolderPath;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("APCA-API-KEY-ID", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", _apiSecret);
    }

    /// <summary>
    /// Pobiera dane DZIENNE (Daily) z Alpaca i zapisuje w formacie CSV dla Lean
    /// </summary>
    public async Task DownloadDailyDataAsync(
        string symbol, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var bars = await FetchBarsFromAlpacaAsync(symbol, "1Day", startDate, endDate, cancellationToken);

        if (bars.Count == 0)
            throw new Exception($"Brak danych dziennych dla symbolu: {symbol}");

        var leanCsvBuilder = new StringBuilder();

        foreach (var bar in bars.OrderBy(b => b.Timestamp))
        {
            // Przeliczenie na czas nowojorski (EST/EDT) - Lean wymaga czasu rynkowego
            var estTime = ConvertToEasternTime(bar.Timestamp);
            var dateStr = estTime.ToString("yyyyMMdd 00:00", CultureInfo.InvariantCulture);

            var row = $"{dateStr}," +
                      $"{bar.Open.ToString(CultureInfo.InvariantCulture)}," +
                      $"{bar.High.ToString(CultureInfo.InvariantCulture)}," +
                      $"{bar.Low.ToString(CultureInfo.InvariantCulture)}," +
                      $"{bar.Close.ToString(CultureInfo.InvariantCulture)}," +
                      $"{bar.Volume}";

            leanCsvBuilder.AppendLine(row);
        }

        var destinationFolder = Path.Combine(_dataFolderPath, "equity", "usa", "daily");
        Directory.CreateDirectory(destinationFolder);

        var filePath = Path.Combine(destinationFolder, $"{symbol.ToLowerInvariant()}.csv");
        await File.WriteAllTextAsync(filePath, leanCsvBuilder.ToString(), cancellationToken);
    }

    /// <summary>
    /// Pobiera dane MINUTOWE (1Min) z Alpaca i zapisuje w formacie paczek ZIP dla Lean
    /// </summary>
    public async Task DownloadMinuteDataAsync(
        string symbol, 
        DateTime startDate, 
        DateTime endDate,
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var bars = await FetchBarsFromAlpacaAsync(symbol, "1Min", startDate, endDate, cancellationToken);

        if (bars.Count == 0)
            throw new Exception($"Brak danych minutowych dla symbolu: {symbol}");

        var symbolLower = symbol.ToLowerInvariant();
        var destinationFolder = Path.Combine(_dataFolderPath, "equity", "usa", "minute", symbolLower);
        Directory.CreateDirectory(destinationFolder);

        // Grupowanie świeczek według DNI w strefie czasowej Nowego Jorku (US Eastern)
        var groupedByDate = bars
            .Select(b => new
            {
                EstTime = ConvertToEasternTime(b.Timestamp),
                Bar = b
            })
            .GroupBy(x => x.EstTime.ToString("yyyyMMdd"));

        foreach (var group in groupedByDate)
        {
            var dateKey = group.Key;
            var zipFilePath = Path.Combine(destinationFolder, $"{dateKey}_trade.zip");
            var csvFileName = $"{dateKey}_{symbolLower}_minute_trade.csv";

            if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

            using var fs = new FileStream(zipFilePath, FileMode.Create);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);
            
            var entry = archive.CreateEntry(csvFileName);
            using var writer = new StreamWriter(entry.Open());

            foreach (var item in group.OrderBy(x => x.EstTime))
            {
                var msSinceMidnight = (long)item.EstTime.TimeOfDay.TotalMilliseconds;
                
                // Lean przelicza ceny w plikach minutowych jako liczby całkowite (* 10 000)
                var open = (long)(item.Bar.Open * 10000m);
                var high = (long)(item.Bar.High * 10000m);
                var low = (long)(item.Bar.Low * 10000m);
                var close = (long)(item.Bar.Close * 10000m);
                var volume = item.Bar.Volume;

                writer.WriteLine($"{msSinceMidnight},{open},{high},{low},{close},{volume}");
            }
        }
    }

    /// <summary>
    /// Pobiera dane z API Alpaca z obsługą stronicyzacji (page_token)
    /// </summary>
    private async Task<List<AlpacaBar>> FetchBarsFromAlpacaAsync(
        string symbol, 
        string timeframe, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken)
    {
        var allBars = new List<AlpacaBar>();
        string? pageToken = null;

        var startIso = startDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var endIso = endDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        do
        {
            var url = $"https://data.alpaca.markets/v2/stocks/bars?" +
                      $"symbols={symbol}" +
                      $"&timeframe={timeframe}" +
                      $"&start={startIso}" +
                      $"&end={endIso}" +
                      $"&limit=1000" +
                      $"&feed=iex"; // 'iex' jest bezpłatnym źródłem danych Alpaca

            if (!string.IsNullOrEmpty(pageToken))
            {
                url += $"&page_token={pageToken}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("bars", out var barsElement) && 
                barsElement.TryGetProperty(symbol, out var symbolBars))
            {
                foreach (var barEl in symbolBars.EnumerateArray())
                {
                    allBars.Add(new AlpacaBar
                    {
                        Timestamp = barEl.GetProperty("t").GetDateTime(),
                        Open = barEl.GetProperty("o").GetDecimal(),
                        High = barEl.GetProperty("h").GetDecimal(),
                        Low = barEl.GetProperty("l").GetDecimal(),
                        Close = barEl.GetProperty("c").GetDecimal(),
                        Volume = barEl.GetProperty("v").GetInt64()
                    });
                }
            }

            pageToken = root.TryGetProperty("next_page_token", out var tokenElement) && 
                        tokenElement.ValueKind == JsonValueKind.String
                ? tokenElement.GetString()
                : null;

        } while (!string.IsNullOrEmpty(pageToken));
        Console.WriteLine($"Pobierano {allBars.Count} dni danych dla symbolu: {symbol}");
        return allBars;
    }

    public async Task EnsureSystemFilesExistAsync(CancellationToken cancellationToken = default)
    {
        var symbolPropsDir = Path.Combine(_dataFolderPath, "symbol-properties");
        var marketHoursDir = Path.Combine(_dataFolderPath, "market-hours");

        Directory.CreateDirectory(symbolPropsDir);
        Directory.CreateDirectory(marketHoursDir);

        var symbolPropsPath = Path.Combine(symbolPropsDir, "symbol-properties-database.csv");
        var marketHoursPath = Path.Combine(marketHoursDir, "market-hours-database.json");

        if (!File.Exists(symbolPropsPath))
        {
            var csv = await _httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv", 
                cancellationToken);
            await File.WriteAllTextAsync(symbolPropsPath, csv, cancellationToken);
        }

        if (!File.Exists(marketHoursPath))
        {
            var json = await _httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json", 
                cancellationToken);
            await File.WriteAllTextAsync(marketHoursPath, json, cancellationToken);
        }
    }

    private static DateTime ConvertToEasternTime(DateTime utcDateTime)
    {
        TimeZoneInfo estZone;
        try
        {
            estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch
        {
            estZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); // Dla linuksa / macOS
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, estZone);
    }

    private record AlpacaBar
    {
        public DateTime Timestamp { get; init; }
        public decimal Open { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal Close { get; init; }
        public long Volume { get; init; }
    }
}