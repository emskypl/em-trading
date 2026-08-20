using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Interfaces;
using EmTrading.Infrastructure.Services;

namespace EmTrading.Presentation;

// Klasa MUSI być partial i dziedziczyć po ObservableObject
public partial class MainViewModel : ObservableObject
{
    private readonly ITradingEngineService _tradingEngineService;
    public IAsyncRelayCommand StartBacktestCommand { get; }
    
    public MainViewModel(ITradingEngineService tradingEngineService)
    {
        _tradingEngineService = tradingEngineService;
        StartBacktestCommand = new AsyncRelayCommand(ExecuteBacktestAsync);
        Title = "Aplikacja Handlowa EmTrading";
    }

    private async Task ExecuteBacktestAsync()
    {
        var downloader = new DataDownloaderService("C:\\TradingData");

        await downloader.EnsureSystemFilesExistAsync();
        await downloader.DownloadDailyDataAsync("aapl");
        await _tradingEngineService.RunBacktestAsync("SmaCrossStrategy", DateTime.Now.AddYears(-1), DateTime.Now);
    }
    
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Gotowy";
    
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        StatusMessage = "Pobieranie danych...";
        
        // Tutaj wywołujesz logikę biznesową, np:
        // var products = await _getProductsUseCase.ExecuteAsync();
        await Task.Delay(2000); // Symulacja pracy

        StatusMessage = "Dane zaktualizowane!";
    }
}