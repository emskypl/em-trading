using System.Reflection;
using EmTrading.Application.Backtesting;
using EmTrading.Application.MarketData;
using EmTrading.Domain.MarketData;
using EmTrading.Infrastructure;
using EmTrading.Presentation;
using NetArchTest.Rules;

namespace EmTrading.Architecture.Tests;

/// <summary>
/// Clean Architecture dependency rule: dependencies point inwards (App → Presentation/Infrastructure → Application → Domain).
/// </summary>
public class LayerDependencyTests
{
    private const string ApplicationNamespace = "EmTrading.Application";
    private const string InfrastructureNamespace = "EmTrading.Infrastructure";
    private const string PresentationNamespace = "EmTrading.Presentation";
    private const string AppNamespace = "EmTrading.App";

    private static readonly Assembly DomainAssembly = typeof(Timeframe).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IBacktestEngine).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureDependencyInjection).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(MainViewModel).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        AssertNoDependency(DomainAssembly,
            ApplicationNamespace, InfrastructureNamespace, PresentationNamespace, AppNamespace,
            "QuantConnect", "Newtonsoft", "Microsoft.Extensions", "Microsoft.EntityFrameworkCore", "System.Windows");
    }

    [Fact]
    public void Application_does_not_depend_on_outer_layers_or_engine()
    {
        AssertNoDependency(ApplicationAssembly,
            InfrastructureNamespace, PresentationNamespace, AppNamespace,
            "QuantConnect", "Newtonsoft", "Microsoft.EntityFrameworkCore", "System.Windows");
    }

    [Fact]
    public void Presentation_does_not_depend_on_infrastructure()
    {
        AssertNoDependency(PresentationAssembly, InfrastructureNamespace, AppNamespace, "QuantConnect", "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_ui()
    {
        AssertNoDependency(InfrastructureAssembly, PresentationNamespace, AppNamespace, "System.Windows", "LiveCharts", "CommunityToolkit.Mvvm");
    }

    [Fact]
    public void Infrastructure_implementations_are_not_public()
    {
        // Outer layers talk to Application ports; concrete adapters stay internal to Infrastructure.
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ImplementInterface(typeof(IBacktestEngine))
            .Or().ImplementInterface(typeof(IBacktestResultReader))
            .Or().ImplementInterface(typeof(IMarketDataDownloader))
            .Should().NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
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
