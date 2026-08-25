using EmTrading.Application.Events;
using EmTrading.Application.Interfaces;
using System.Diagnostics;
using EmTrading.Infrastructure.Helpers;

namespace EmTrading.Infrastructure.Engine;

public class LeanTradingEngineService : ITradingEngineService
{
    public event EventHandler<BacktestProgressEventArgs>? ProgressUpdated;
    public event EventHandler<TradeExecutedEventArgs>? TradeExecuted;

    public async Task RunBacktestAsync(string strategyName,
        DateTime start,
        DateTime end,
        string symbol,
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        if (strategyName == null) throw new ArgumentNullException(nameof(strategyName));

        await Task.Run(async () =>
        {
            var engineExePath = GetEngineExecutablePath();
            progress.Report(1);
            if (!File.Exists(engineExePath))
            {
                throw new FileNotFoundException($"Nie znaleziono pliku silnika: {engineExePath}");
            }

            var args = $"\"{strategyName}\" \"{start:yyyy-MM-dd}\" \"{end:yyyy-MM-dd}\" \"{symbol}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = engineExePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Jeśli nie chcesz na razie czytać logów w WPF, najlepiej ustawić na false, 
                // co całkowicie eliminuje problem zakleszczenia bufora Windows!
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            using var process = new Process { StartInfo = startInfo };

            process.Start();
            progress.Report(10);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new Exception($"Silnik zakończył pracę z błędem (Kod zakończenia: {process.ExitCode})");
            }

            progress.Report(30);

            Directory.CreateDirectory(Path.Combine(PathHelper.GetSharedDataFolderPath(), "results"));
            var resultJsonPath = Path.Combine(Path.Combine(PathHelper.GetSharedDataFolderPath(), "results"),
                $"{strategyName}.json");

            string jsonContent = await File.ReadAllTextAsync(resultJsonPath, cancellationToken);


            progress.Report(100);
        }, cancellationToken);
    }

    private static string GetEngineExecutablePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var localPath = Path.Combine(baseDir, "EmTrading.EngineRunner.exe");
        if (File.Exists(localPath)) return localPath;

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

        var relativeDevPath = Path.Combine(
            baseDir,
            "..", "..", "..", "..",
            "EmTrading.EngineRunner", "bin", configuration, "net10.0",
            "EmTrading.EngineRunner.exe"
        );

        return Path.GetFullPath(relativeDevPath);
    }
}