using System;
using System.IO;
using System.Windows;
using EmTrading.Application;
using EmTrading.Infrastructure;
using EmTrading.Infrastructure.Helpers;
using EmTrading.Infrastructure.Services;
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
        
        //Lean folders
        string dataFolderPath = PathHelper.GetSharedDataFolderPath();
        LeanDataFolderInitializer.Initialize(dataFolderPath);
        
        
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

