using System.Globalization;
using System.Text;

namespace EmTrading.Infrastructure.Services;

public class DataDownloaderService
{
    private readonly HttpClient _httpClient;
    private readonly string _dataFolderPath;

    public DataDownloaderService(string dataFolderPath = "C:\\TradingData")
    {
        _httpClient = new HttpClient();
        _dataFolderPath = dataFolderPath;
    }

    /// <summary>
    /// Pobiera dane dzienne (Daily) dla symbolu (np. "AAPL", "MSFT", "PKN.PL") i zapisuje do CSV dla Lean
    /// </summary>
    public async Task DownloadDailyDataAsync(string symbol, CancellationToken cancellationToken = default)
    {
        // 1. Stooq URL dla danych dziennych w formacie CSV
        var ticker = symbol.ToLowerInvariant().EndsWith(".us") || symbol.Contains(".") 
            ? symbol.ToLowerInvariant() 
            : $"{symbol.ToLowerInvariant()}.us";

        var url = $"https://stooq.com/q/d/l/?s={ticker}&i=d";

        // 2. Pobranie surowego pliku CSV
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var csvContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
            throw new Exception($"Brak danych dla symbolu: {symbol}");

        // 3. Konwersja do formatu akceptowanego przez Lean (bez nagłówków, ceny w pipsach/centach * 10000 dla akcji)
        var leanCsvBuilder = new StringBuilder();

        // Pomijamy nagłówek Stooq (Date,Open,High,Low,Close,Volume)
        foreach (var line in lines.Skip(1))
        {
            var cols = line.Split(',');
            if (cols.Length < 6) continue;

            // Stooq podaje datę w formacie YYYY-MM-DD
            if (!DateTime.TryParseExact(cols[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            var open = ParseDecimal(cols[1]);
            var high = ParseDecimal(cols[2]);
            var low = ParseDecimal(cols[3]);
            var close = ParseDecimal(cols[4]);
            var volume = ParseDecimal(cols[5]);

            // Format Lean Daily Equity: Date (YYYYMMDD 00:00), Open, High, Low, Close, Volume
            // Ceny w Lean dla akcji przelicza się w skali standardowej (lub pomnożone przez 10000 w zależności od rozdzielczości)
            var formattedDate = date.ToString("yyyyMMdd 00:00", CultureInfo.InvariantCulture);
            var row = $"{formattedDate},{open.ToString(CultureInfo.InvariantCulture)},{high.ToString(CultureInfo.InvariantCulture)},{low.ToString(CultureInfo.InvariantCulture)},{close.ToString(CultureInfo.InvariantCulture)},{(long)volume}";

            leanCsvBuilder.AppendLine(row);
        }

        // 4. Utworzenie struktury katalogów Lean i zapis pliku
        var destinationFolder = Path.Combine(_dataFolderPath, "equity", "usa", "daily");
        Directory.CreateDirectory(destinationFolder);

        var filePath = Path.Combine(destinationFolder, $"{symbol.ToLowerInvariant()}.csv");
        await File.WriteAllTextAsync(filePath, leanCsvBuilder.ToString(), cancellationToken);
    }
    
    public async Task EnsureSystemFilesExistAsync(CancellationToken cancellationToken = default)
    {
        var symbolPropsDir = Path.Combine(_dataFolderPath, "symbol-properties");
        var marketHoursDir = Path.Combine(_dataFolderPath, "market-hours");

        Directory.CreateDirectory(symbolPropsDir);
        Directory.CreateDirectory(marketHoursDir);

        var symbolPropsPath = Path.Combine(symbolPropsDir, "symbol-properties-database.csv");
        var marketHoursPath = Path.Combine(marketHoursDir, "market-hours-database.json");

        // Pobierz symbol-properties jeśli nie istnieje
        if (!File.Exists(symbolPropsPath))
        {
            var csv = await _httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv", 
                cancellationToken);
            await File.WriteAllTextAsync(symbolPropsPath, csv, cancellationToken);
        }

        // Pobierz market-hours jeśli nie istnieje
        if (!File.Exists(marketHoursPath))
        {
            var json = await _httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json", 
                cancellationToken);
            await File.WriteAllTextAsync(marketHoursPath, json, cancellationToken);
        }
    }

    private static decimal ParseDecimal(string value)
    {
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result);
        return result;
    }
}