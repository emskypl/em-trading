using System.Windows;
using EmTrading.Application;
using EmTrading.Infrastructure;
using EmTrading.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace EmTrading.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static IServiceProvider ServiceProvider { get; set; } = null;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        
        services.AddTransient<MainViewModel>();
        services.AddSingleton<MainWindow>(provider => new MainWindow
        {
            DataContext = provider.GetRequiredService<MainViewModel>()
        });
        
        ServiceProvider = services.BuildServiceProvider();
        
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
    
}

