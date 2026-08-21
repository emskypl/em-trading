using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Interfaces;
using EmTrading.Infrastructure.Services;

namespace EmTrading.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly ITradingEngineService _tradingEngineService;
    
    public MainViewModel(ITradingEngineService tradingEngineService)
    {
        Title = "Trading Application EmTrading";
        _tradingEngineService = tradingEngineService;
    }
    
    [RelayCommand]
    private async Task ExecuteBacktestAsync(string symbol)
    {
        await _tradingEngineService.RunBacktestAsync("SmaCrossStrategy", DateTime.Now.AddYears(-1), DateTime.Now, symbol);
    }
    
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Gotowy";
    
    [RelayCommand]
    private async Task DownloadDataAsync(string symbol)
    {
        var downloader = new DataDownloaderService("PKPS4AAP4UX4NVC56SVANZMEOT","4d6nrJd4qxvGYCwMJn1DX3sUf55i1dWix9vScJB2Z7Eq","C:\\TradingData");
        await downloader.EnsureSystemFilesExistAsync();
        await downloader.DownloadMinuteDataAsync(symbol, DateTime.Now.AddYears(-1), DateTime.Now);
        // await downloader.DownloadDailyDataAsync("aapl");
        // await LeanDataConverterService.ConvertDukascopyToLeanMinute("MMMUSUSD",@"D:\QuantDataManager125\export\2026.8.21MMMUSUSD-M1-No Session.csv");
    }
}