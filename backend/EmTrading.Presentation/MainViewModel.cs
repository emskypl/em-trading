using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Interfaces;
using EmTrading.Domain.Alpaca;
using EmTrading.Domain.Lean;
using EmTrading.Infrastructure.Helpers;
using EmTrading.Infrastructure.Services;
using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;

namespace EmTrading.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly ITradingEngineService _tradingEngineService;
    
    [ObservableProperty] 
    private int _currentProgress;
    
    [ObservableProperty]
    private bool _progressVisible;
    
    [ObservableProperty] 
    private string _statusMessage = "Ready";

    // Backtest results
    [ObservableProperty] private string _compoundingReturn = "0%";
    [ObservableProperty] private string _maxDrawdown = "0%";
    [ObservableProperty] private string _winRate = "0%";
    [ObservableProperty] private string _sharpeRatio = "0";
    [ObservableProperty] private string _totalTrades = "0";
    
    // Livechart
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
        ProgressVisible = true;
        CurrentProgress = 0;
        StatusMessage = $"Running backtest for {symbol}...";
        
        var progress = new Progress<int>(percent => CurrentProgress = percent);

        try
        {
            await _tradingEngineService.RunBacktestAsync(
                "SmaCrossStrategy", 
                DateTime.Now.AddYears(-1), 
                DateTime.Now, 
                symbol,
                progress);
            
            CurrentProgress = 100;
            StatusMessage = "Backtest completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backtest failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadDataAsync(string symbol)
    {
        CurrentProgress = 0;
        StatusMessage = $"Downloading data for {symbol}...";

        var progress = new Progress<int>(percent => CurrentProgress = percent);

        try
        {
            var downloader = new DataDownloaderService(
                PathHelper.GetSharedDataFolderPath(),
                "PKPS4AAP4UX4NVC56SVANZMEOT",
                "4d6nrJd4qxvGYCwMJn1DX3sUf55i1dWix9vScJB2Z7Eq");
            
            await downloader.EnsureSystemFilesExistAsync();

            await downloader.DownloadDataAsync(
                symbol,
                DateTime.Now.AddYears(-1),
                DateTime.Now,
                progress,
                AlpacaTimeframe.Minute1);

            CurrentProgress = 100;
            StatusMessage = "Download completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadBacktestResultsAsync(string symbol)
    {
        CurrentProgress = 0;
        string filePath = Path.Combine(PathHelper.GetResultsFolderPath(), symbol, $"2025-08-25_{symbol}_SmaCrossStrategy.json");

        if (!File.Exists(filePath)) return;
        
        
        
        string jsonContent = await File.ReadAllTextAsync(filePath);
        var result = JsonConvert.DeserializeObject<LeanResult>(jsonContent);

        if (result == null) return;

        CompoundingReturn = result.Statistics.GetValueOrDefault("Compounding Annual Return", "0%");
        MaxDrawdown = result.Statistics.GetValueOrDefault("Drawdown", "0%");
        WinRate = result.Statistics.GetValueOrDefault("Win Rate", "0%");
        SharpeRatio = result.Statistics.GetValueOrDefault("Sharpe Ratio", "0");
        TotalTrades = result.Statistics.GetValueOrDefault("Total Orders", "0");

        if (result.Charts.ContainsKey("Strategy Equity"))
        {
            var equityChart = result.Charts["Strategy Equity"];
            if (equityChart.Series.ContainsKey("Equity"))
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

                CurrentProgress = 50;
                var chartValues = new ChartValues<double>(points.Select(p => p.Value));

                EquitySeriesCollection = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Kapitał ($)",
                        Values = chartValues,
                        PointGeometry = null,
                        StrokeThickness = 2
                    }
                };

                // Etykiety osi X (daty transakcji)
                ChartLabels = points.Select(p => p.DateTime.ToString("yyyy-MM-dd")).ToList();
            }
        }
        CurrentProgress = 100;
        StatusMessage = "Results loaded.";
    }
}