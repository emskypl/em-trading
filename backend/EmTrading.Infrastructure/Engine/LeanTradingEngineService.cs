using EmTrading.Application.Events;
using EmTrading.Application.Interfaces;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Lean.Engine;
using QuantConnect.Util;
using System.Reflection;
using QuantConnect.Algorithm.CSharp;

namespace EmTrading.Infrastructure.Engine;

public class LeanTradingEngineService : ITradingEngineService
{
    public async Task RunBacktestAsync(string strategyName, DateTime start, DateTime end, string symbol, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var currentAssemblyLocation = typeof(LeanTradingEngineService).Assembly.Location;

            // Basic LEAN config
            Config.Set("environment", "backtesting");
            Config.Set("algorithm-type-name", strategyName);
            Config.Set("algorithm-location", currentAssemblyLocation);
            Config.Set("result-handler", typeof(WpfResultHandler).AssemblyQualifiedName);
            Config.Set("data-folder", "C:\\TradingData");

            // Pass dates into config (LEAN will pick them up)
            Config.Set("start-date", start.ToString("yyyy-MM-dd"));
            Config.Set("end-date", end.ToString("yyyy-MM-dd"));

            // IMPORTANT: provide algorithm parameters so QCAlgorithm.GetParameter("symbol") returns the symbol
            // You can pass multiple parameters as "k1=v1,k2=v2"
            Config.Set("algorithm-parameters", $"symbol={symbol}");

            bool liveMode = Config.GetBool("live-mode", false);

            var composer = Composer.Instance;
            var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(composer);

            // Ensure there is no real lean manager in this context
            var leanManagerField = typeof(LeanEngineSystemHandlers)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => typeof(ILeanManager).IsAssignableFrom(f.FieldType));

            leanManagerField?.SetValue(systemHandlers, new NullLeanManager());

            systemHandlers.Initialize();

            var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(composer);

            // Obtain job from the queue (existing flow) - fallback to assembly path if needed
            var job = systemHandlers.JobQueue.NextJob(out var algorithmPath);

            if (string.IsNullOrEmpty(algorithmPath) || !File.Exists(algorithmPath))
            {
                algorithmPath = currentAssemblyLocation;
            }

            var algorithmManager = new AlgorithmManager(liveMode, job);

            var engine = new QuantConnect.Lean.Engine.Engine(systemHandlers, algorithmHandlers, liveMode: false);

            engine.Run(
                job,
                algorithmManager,
                algorithmPath,
                WorkerThread.Instance
            );

        }, cancellationToken);
    }

    public event EventHandler<BacktestProgressEventArgs>? ProgressUpdated;
    public event EventHandler<TradeExecutedEventArgs>? TradeExecuted;
}