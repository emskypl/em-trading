using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EmTrading.Presentation;

// Klasa MUSI być partial i dziedziczyć po ObservableObject
public partial class MainViewModel : ObservableObject
{
    // Przykład wstrzykiwania logiki z warstwy Application przez konstruktor
    // private readonly IGetProductsUseCase _getProductsUseCase;
    // public MainViewModel(IGetProductsUseCase getProductsUseCase) { ... }

    public MainViewModel()
    {
        // Tutaj opcjonalnie inicjalizujesz stan początkowy
        Title = "Aplikacja Handlowa EmTrading";
    }

    // 1. WŁAŚCIWOŚĆ (Property)
    // Generator kodu automatycznie utworzy publiczną właściwość "Title" (z wielkiej litery)
    // oraz zaimplementuje dla niej powiadomienie widoku o zmianie (INotifyPropertyChanged)
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Gotowy";

    // 2. KOMENDA (ICommand) - odpowiednik zdarzenia kliknięcia przycisku
    // Generator automatycznie utworzy właściwość "RefreshDataCommand"
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