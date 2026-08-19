using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using EmTrading.Application;
using EmTrading.Infrastructure;
using EmTrading.Presentation;
using EmTrading.Wpf.Controls;

namespace EmTrading.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var services = new ServiceCollection();

        services.AddApplicationServices();
        
        // // 2. Rejestracja ViewModels z warstwy Prezentacji
        services.AddTransient<MainViewModel>();
        
        // 3. Rejestracja Widoków z warstwy WPF (Główne okno)
        services.AddSingleton<MainWindow>(provider => new MainWindow
        {
            // Automatyczne wstrzykiwanie ViewModelu do DataContext okna
            DataContext = provider.GetRequiredService<MainViewModel>()
        });

        // Budowanie kontenera
        ServiceProvider = services.BuildServiceProvider();

        // Ręczne pobranie głównego okna z DI i jego wyświetlenie
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
    
}

