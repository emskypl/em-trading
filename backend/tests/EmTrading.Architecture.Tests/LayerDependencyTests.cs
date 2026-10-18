using System.Reflection;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.Strategies;
using EmTrading.Application.MarketData;
using EmTrading.Domain.MarketData;
using EmTrading.Infrastructure;
using EmTrading.Infrastructure.Alpaca;
using EmTrading.Infrastructure.Lean;
using EmTrading.Infrastructure.MarketData;
using EmTrading.Presentation.Shell;
using EmTrading.Wpf.Controls;
using EmTrading.Strategies;
using NetArchTest.Rules;

namespace EmTrading.Architecture.Tests;

/// <summary>
/// Clean Architecture dependency rule: dependencies point inwards
/// (App → Presentation / Infrastructure adapters → Application → Domain), and adapters stay independent of each other.
/// </summary>
public class LayerDependencyTests
{
    private const string ApplicationNamespace = "EmTrading.Application";
    private const string InfrastructureNamespace = "EmTrading.Infrastructure";
    private const string AlpacaNamespace = "EmTrading.Infrastructure.Alpaca";
    private const string LeanNamespace = "EmTrading.Infrastructure.Lean";
    private const string StrategiesNamespace = "EmTrading.Strategies";
    private const string PresentationNamespace = "EmTrading.Presentation";
    private const string AppNamespace = "EmTrading.App";
    private static readonly string[] UiFrameworks = ["System.Windows", "LiveCharts", "CommunityToolkit.Mvvm"];

    private static readonly Assembly DomainAssembly = typeof(Timeframe).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IBacktestEngine).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureDependencyInjection).Assembly;
    private static readonly Assembly AlpacaAssembly = typeof(AlpacaDependencyInjection).Assembly;
    private static readonly Assembly LeanAssembly = typeof(LeanDependencyInjection).Assembly;
    private static readonly Assembly StrategiesAssembly = typeof(StrategyBase).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(ShellViewModel).Assembly;
    private static readonly Assembly ControlsAssembly = typeof(KpiCard).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        AssertNoDependency(DomainAssembly,
            ApplicationNamespace, InfrastructureNamespace, PresentationNamespace, AppNamespace, StrategiesNamespace,
            "QuantConnect", "Newtonsoft", "Microsoft.Extensions", "Microsoft.EntityFrameworkCore", "System.Windows");
    }

    [Fact]
    public void Application_does_not_depend_on_outer_layers_or_engine()
    {
        AssertNoDependency(ApplicationAssembly,
            InfrastructureNamespace, PresentationNamespace, AppNamespace, StrategiesNamespace,
            "QuantConnect", "Newtonsoft", "Microsoft.EntityFrameworkCore", "System.Windows");
    }

    [Fact]
    public void Presentation_does_not_depend_on_infrastructure()
    {
        AssertNoDependency(PresentationAssembly,
            InfrastructureNamespace, AppNamespace, StrategiesNamespace, "QuantConnect", "Microsoft.EntityFrameworkCore", "Serilog");
    }

    [Fact]
    public void Controls_library_knows_nothing_about_the_app()
    {
        // Reusable controls take their own simple types; view models map application data onto them.
        AssertNoDependency(ControlsAssembly,
            "EmTrading.Domain", ApplicationNamespace, InfrastructureNamespace, PresentationNamespace, AppNamespace, StrategiesNamespace);
    }

    [Fact]
    public void Shared_infrastructure_knows_no_adapter_or_engine()
    {
        AssertNoDependency(InfrastructureAssembly,
            [AlpacaNamespace, LeanNamespace, StrategiesNamespace, "QuantConnect", PresentationNamespace, AppNamespace, .. UiFrameworks]);
    }

    [Fact]
    public void Adapters_are_independent_of_each_other_and_of_the_ui()
    {
        AssertNoDependency(AlpacaAssembly,
            [LeanNamespace, StrategiesNamespace, "QuantConnect", "Microsoft.EntityFrameworkCore", PresentationNamespace, AppNamespace, .. UiFrameworks]);
        AssertNoDependency(LeanAssembly,
            [AlpacaNamespace, "Microsoft.EntityFrameworkCore", PresentationNamespace, AppNamespace, .. UiFrameworks]);
    }

    [Fact]
    public void Strategies_depend_only_on_the_engine()
    {
        // They run inside the Lean engine process and must not pull in the app.
        AssertNoDependency(StrategiesAssembly,
            ApplicationNamespace, "EmTrading.Domain", InfrastructureNamespace, PresentationNamespace, AppNamespace,
            "Microsoft.EntityFrameworkCore", "Microsoft.Extensions");
    }

    [Fact]
    public void Port_implementations_are_not_public()
    {
        // Outer layers talk to Application ports and infrastructure seams; concrete adapters stay internal.
        Type[] ports =
        [
            typeof(IBacktestEngine), typeof(IBacktestResultReader), typeof(IMarketDataDownloader), typeof(IStrategyCatalog),
            typeof(IHistoricalBarSource), typeof(IMarketDataStore)
        ];

        foreach (var assembly in new[] { InfrastructureAssembly, AlpacaAssembly, LeanAssembly })
        {
            var publicImplementations = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && (t.IsPublic || t.IsNestedPublic))
                .Where(t => ports.Any(port => port.IsAssignableFrom(t)))
                .Select(t => t.FullName);

            Assert.Empty(publicImplementations);
        }
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
