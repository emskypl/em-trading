using System.Globalization;
using System.IO.Compression;

namespace EmTrading.Infrastructure.Services;

public class LeanDataConverterService
{
    /// <summary>
    /// Przetwarza jeden duży plik CSV minutowy na dzienne pliki ZIP w formacie Lean
    /// </summary>
    public static Task ConvertDukascopyToLeanMinute(string symbol, string inputCsvPath, string dataFolder = "C:\\TradingData")
    {
        symbol = symbol.ToLowerInvariant();
        var destinationFolder = Path.Combine(dataFolder, "equity", "usa", "minute", symbol);
        Directory.CreateDirectory(destinationFolder);

        // Pomijamy nagłówek, jeśli istnieje (zależnie od tego, jak wyeksportował QDM)
        var lines = File.ReadAllLines(inputCsvPath).Where(l => char.IsDigit(l[0]));
        
        var groupedByDate = new Dictionary<string, List<string>>();

        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length < 6) continue;

            // Parsowanie daty z formatu: yyyy-MM-dd HH:mm:ss
            if (!DateTime.TryParseExact(parts[0].Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                continue;

            var dateStr = dt.ToString("yyyyMMdd");
            var msSinceMidnight = (long)dt.TimeOfDay.TotalMilliseconds;

            // Lean dla danych akcyjnych przechowuje ceny jako liczby całkowite (pomnożone przez 10 000)
            var open = (long)(decimal.Parse(parts[1], CultureInfo.InvariantCulture) * 10000m);
            var high = (long)(decimal.Parse(parts[2], CultureInfo.InvariantCulture) * 10000m);
            var low = (long)(decimal.Parse(parts[3], CultureInfo.InvariantCulture) * 10000m);
            var close = (long)(decimal.Parse(parts[4], CultureInfo.InvariantCulture) * 10000m);
            var volume = (long)decimal.Parse(parts[5], CultureInfo.InvariantCulture);

            var leanRow = $"{msSinceMidnight},{open},{high},{low},{close},{volume}";

            if (!groupedByDate.ContainsKey(dateStr))
            {
                groupedByDate[dateStr] = new List<string>();
            }
            groupedByDate[dateStr].Add(leanRow);
        }

        // Zapis do struktury dziennych archiwów ZIP
        foreach (var kvp in groupedByDate)
        {
            var date = kvp.Key;
            var zipFilePath = Path.Combine(destinationFolder, $"{date}_trade.zip");
            var csvFileName = $"{date}_{symbol}_minute_trade.csv";

            // Usunięcie starego pliku, jeśli już istnieje
            if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

            using (var fs = new FileStream(zipFilePath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(csvFileName);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    foreach (var row in kvp.Value)
                    {
                        writer.WriteLine(row);
                    }
                }
            }
        }
        return Task.CompletedTask;
    }
}