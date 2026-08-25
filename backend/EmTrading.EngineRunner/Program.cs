using QuantConnect.Configuration;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Lean.Engine;
using QuantConnect.Util;
using System.Reflection;
using EmTrading.Infrastructure.Helpers;

namespace EmTrading.EngineRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Błąd: Niewystarczająca liczba argumentów. Wymagane: <StrategyName> <Start> <End> <Symbol>");
                Environment.Exit(1);
                return;
            }
            
            string strategyName = args[0];
            string start = args[1];
            string end = args[2];
            string symbol = args[3];

            try
            {
                var currentAssemblyLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmTrading.Infrastructure.dll");
                
                Config.Set("environment", "backtesting");
                Config.Set("algorithm-type-name", strategyName);
                Config.Set("algorithm-location", currentAssemblyLocation);
                Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");

                var dataFolderPath = PathHelper.GetSharedDataFolderPath();
                Directory.CreateDirectory(dataFolderPath);
                Config.Set("data-folder", dataFolderPath);
                Config.Set("start-date", start);
                Config.Set("end-date", end);
                Config.Set("algorithm-parameters", $"symbol={symbol}");

                bool liveMode = Config.GetBool("live-mode", false);

                var composer = Composer.Instance;
                var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(composer);
                
                var leanManagerField = typeof(LeanEngineSystemHandlers)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => typeof(ILeanManager).IsAssignableFrom(f.FieldType));
                
                leanManagerField?.SetValue(systemHandlers, new EmTrading.Infrastructure.Engine.NullLeanManager());

                systemHandlers.Initialize();

                var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(composer);
                Console.WriteLine($"[Engine] Config algorithm-parameters = {Config.Get("algorithm-parameters")}");

                var job = systemHandlers.JobQueue.NextJob(out var algorithmPath);
                
                if (job != null)
                {
                    try
                    {
                        var prop = job.GetType().GetProperty("Parameters")
                                   ?? job.GetType().GetProperty("AlgorithmParameters")
                                   ?? job.GetType().GetProperty("ParametersData");

                        if (prop != null)
                        {
                            var value = prop.GetValue(job);
                            if (value is IDictionary<string, string> dict)
                            {
                                dict["symbol"] = symbol;
                                Console.WriteLine("[Engine] Injected symbol into job.Parameters dictionary");
                            }
                            else
                            {
                                var newDict = new Dictionary<string, string> { ["symbol"] = symbol };
                                if (prop.PropertyType.IsAssignableFrom(newDict.GetType()))
                                {
                                    prop.SetValue(job, newDict);
                                    Console.WriteLine("[Engine] Set job.Parameters to newly created dictionary with symbol");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Engine] Failed to inject symbol into job parameters: {ex}");
                    }
                }

                if (string.IsNullOrEmpty(algorithmPath) || !File.Exists(algorithmPath))
                {
                    algorithmPath = currentAssemblyLocation;
                }

                var algorithmManager = new AlgorithmManager(liveMode, job);
                var engine = new QuantConnect.Lean.Engine.Engine(systemHandlers, algorithmHandlers, liveMode: false);
                
                using var workerThread = (WorkerThread)Activator.CreateInstance(typeof(WorkerThread), nonPublic: true)!;
                
                engine.Run(
                    job,
                    algorithmManager,
                    algorithmPath,
                    workerThread
                );

                Console.WriteLine("BACKTEST ZAKONCZONY");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL_ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        private static string GetDataFolderPath(string dataFolderPath, string appPath)
        {
            if (!Directory.Exists(dataFolderPath))
            {
                var devDataPath = Path.GetFullPath(Path.Combine(appPath, @"..\..\..\..\TradingData"));
                if (Directory.Exists(devDataPath))
                {
                    dataFolderPath = devDataPath;
                }
            }

            return dataFolderPath;
        }
    }
}