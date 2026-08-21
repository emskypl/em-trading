using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Interfaces;
using EmTrading.Domain.Lean;
using EmTrading.Infrastructure.Services;
using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;

namespace EmTrading.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly ITradingEngineService _tradingEngineService;

    // 1. Właściwości tekstowe dla widoku WPF
    [ObservableProperty] private string _compoundingReturn = "0%";
    [ObservableProperty] private string _maxDrawdown = "0%";
    [ObservableProperty] private string _winRate = "0%";
    [ObservableProperty] private string _sharpeRatio = "0";
    [ObservableProperty] private string _totalTrades = "0";

    // 2. Właściwości dla wykresu LiveCharts
    [ObservableProperty] private SeriesCollection _equitySeriesCollection;
    [ObservableProperty] private List<string> _chartLabels;
    [ObservableProperty] private string _title = string.Empty;

    public MainViewModel(ITradingEngineService tradingEngineService)
    {
        Title = "Trading Application EmTrading";
        _tradingEngineService = tradingEngineService;
    }

    [RelayCommand]
    private async Task ExecuteBacktestAsync(string symbol)
    {
        // debug/log to confirm symbol coming from UI
        System.Diagnostics.Debug.WriteLine($"ExecuteBacktestAsync symbol='{symbol}'");
        await _tradingEngineService.RunBacktestAsync("SmaCrossStrategy", DateTime.Now.AddYears(-1), DateTime.Now,
            symbol);
        System.Diagnostics.Debug.WriteLine($"ExecuteBacktestAsync symbol='{symbol}'");
    }

    [RelayCommand]
    private async Task DownloadDataAsync(string symbol)
    {
        var downloader = new DataDownloaderService("PKPS4AAP4UX4NVC56SVANZMEOT",
            "4d6nrJd4qxvGYCwMJn1DX3sUf55i1dWix9vScJB2Z7Eq", "C:\\TradingData");
        await downloader.EnsureSystemFilesExistAsync();
        await downloader.DownloadMinuteDataAsync(symbol, DateTime.Now.AddYears(-1), DateTime.Now);
        // await downloader.DownloadDailyDataAsync("aapl");
        // await LeanDataConverterService.ConvertDukascopyToLeanMinute("MMMUSUSD",@"D:\QuantDataManager125\export\2026.8.21MMMUSUSD-M1-No Session.csv");
    }

    [RelayCommand]
    private void LoadBacktestResults()
    {
        // Podaj realną ścieżkę, gdzie Twój DownloadData/Lean Engine zapisuje plik JSON
        string filePath = @"D:\development\my_projects\em-trading-app\backend\EmTrading.App\bin\Debug\net10.0-windows\SmaCrossStrategy.json";

        if (!File.Exists(filePath)) return;

        string jsonContent = File.ReadAllText(filePath);
        var result = JsonConvert.DeserializeObject<LeanResult>(jsonContent);

        if (result == null) return;

        // --- A. ŁADOWANIE STATYSTYK TEKSTOWYCH ---
        if (result.Statistics != null)
        {
            CompoundingReturn = result.Statistics.GetValueOrDefault("Compounding Annual Return", "0%");
            MaxDrawdown = result.Statistics.GetValueOrDefault("Drawdown", "0%");
            WinRate = result.Statistics.GetValueOrDefault("Win Rate", "0%");
            SharpeRatio = result.Statistics.GetValueOrDefault("Sharpe Ratio", "0");
            TotalTrades = result.Statistics.GetValueOrDefault("Total Orders", "0");
        }

        // --- B. ŁADOWANIE I KONWERSJA DANYCH DO WYKRESU ---
        // Wyciągamy wykres "Strategy Equity" i serię "Equity"
        if (result.Charts != null && result.Charts.ContainsKey("Strategy Equity"))
        {
            var equityChart = result.Charts["Strategy Equity"];
            if (equityChart.Series != null && equityChart.Series.ContainsKey("Equity"))
            {
                var rawValues = equityChart.Series["Equity"].Values;

                var points = new List<ChartPointModel>();
                foreach (var val in rawValues)
                {
                    if (val.Count >= 2)
                    {
                        // Konwersja LEAN Unix Timestamp (sekundy) na DateTime
                        System.DateTime dt = System.DateTime.UnixEpoch.AddSeconds(val[0]).ToLocalTime();
                        points.Add(new ChartPointModel { DateTime = dt, Value = val[1] });
                    }
                }

                // Przygotowanie danych dla LiveCharts
                var chartValues = new ChartValues<double>(points.Select(p => p.Value));

                EquitySeriesCollection = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Kapitał ($)",
                        Values = chartValues,
                        PointGeometry = null, // wyłącza kropki na linii (zwiększa wydajność)
                        StrokeThickness = 2
                    }
                };

                // Etykiety osi X (daty transakcji)
                ChartLabels = points.Select(p => p.DateTime.ToString("yyyy-MM-dd")).ToList();
            }
        }
    }
}