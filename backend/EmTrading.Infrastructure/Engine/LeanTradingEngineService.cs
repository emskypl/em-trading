using EmTrading.Application.Events;
using EmTrading.Application.Interfaces;
using System.Diagnostics;

namespace EmTrading.Infrastructure.Engine;

public class LeanTradingEngineService : ITradingEngineService
{
    public event EventHandler<BacktestProgressEventArgs>? ProgressUpdated;
    public event EventHandler<TradeExecutedEventArgs>? TradeExecuted;

    public async Task RunBacktestAsync(string strategyName, DateTime start, DateTime end, string symbol,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(async () =>
        {
            var engineExePath = Path.Combine(@"D:\development\my_projects\em-trading-app\backend\EmTrading.EngineRunner\bin\Debug\net10.0", "EmTrading.EngineRunner.exe");

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
            
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new Exception($"Silnik zakończył pracę z błędem (Kod zakończenia: {process.ExitCode})");
            }

            var resultJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{strategyName}.json");
            if (File.Exists(resultJsonPath))
            {
                string jsonContent = await File.ReadAllTextAsync(resultJsonPath, cancellationToken);
            }

        }, cancellationToken);
    }
}