using System.Windows;
using System.Windows.Threading;
using EmTrading.Application;
using EmTrading.Infrastructure;
using EmTrading.Infrastructure.Alpaca;
using EmTrading.Infrastructure.Lean;
using EmTrading.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EmTrading.App;

/// <summary>
/// Composition root. The Generic Host provides configuration (appsettings, user secrets, environment variables),
/// DI, Serilog logging and hosted background services.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

        try
        {
            _host = CreateHost(e.Args);
            await _host.StartAsync();

            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            Logger?.LogCritical(ex, "Application failed to start");
            MessageBox.Show(ex.Message, "EmTrading failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            // Run off the UI thread so a hosted service awaiting on the dispatcher cannot deadlock shutdown.
            Task.Run(() => _host.StopAsync(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IHost CreateHost(string[] args)
    {
        var settings = new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(App).Assembly.GetName().Name
        };
#if DEBUG
        // Development loads appsettings.Development.json and user secrets.
        settings.EnvironmentName ??= Environments.Development;
#endif

        var builder = Host.CreateApplicationBuilder(settings);

        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        // Clean Architecture composition: the app layer, shared infrastructure, one adapter per external system, the UI.
        builder.Services
            .AddApplicationServices()
            .AddInfrastructureServices(builder.Configuration)
            .AddAlpacaMarketData(builder.Configuration)
            .AddLeanEngine(builder.Configuration)
            .AddPresentationServices();

        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    private ILogger<App>? Logger => _host?.Services.GetService<ILogger<App>>();

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger?.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Logger?.LogCritical(args.ExceptionObject as Exception, "Unhandled exception, terminating: {Terminating}", args.IsTerminating);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger?.LogError(e.Exception, "Unhandled UI exception");
        MessageBox.Show(e.Exception.Message, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
