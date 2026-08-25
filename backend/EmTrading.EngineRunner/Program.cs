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
                
                var dataFolderPath = InitializeDataAndResultFolders(symbol, out var resultsFolderPath, out var resultsDestinationFolderPath);

                Config.Set("environment", "backtesting");
                Config.Set("algorithm-type-name", strategyName);
                Config.Set("algorithm-location", currentAssemblyLocation);
                Config.Set("data-folder", dataFolderPath);
                
                Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
                Config.Set("results-destination-folder", resultsDestinationFolderPath);

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
                    // Ustawienie BacktestId pozwala wygenerować plik <strategyName>.json w result-destination-folder
                    if (job is QuantConnect.Packets.BacktestNodePacket backtestJob)
                    {
                        backtestJob.BacktestId = start + "_" + symbol + "_" + strategyName;
                    }

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

                Console.WriteLine($"BACKTEST ZAKONCZONY. Plik JSON zapisany w: {Path.Combine(resultsFolderPath, strategyName + ".json")}");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL_ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        private static string InitializeDataAndResultFolders(string symbol, out string resultsFolderPath,
            out string resultsDestinationFolderPath)
        {
            var dataFolderPath = PathHelper.GetSharedDataFolderPath();
            Directory.CreateDirectory(dataFolderPath);
            var baseDirectory = Path.GetDirectoryName(dataFolderPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            resultsFolderPath = Path.Combine(baseDirectory, "Results");
            Directory.CreateDirectory(resultsFolderPath);
            resultsDestinationFolderPath = Path.Combine(resultsFolderPath, symbol);
            Directory.CreateDirectory(resultsDestinationFolderPath);
            return dataFolderPath;
        }
    }
}