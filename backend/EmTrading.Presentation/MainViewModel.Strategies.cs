using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Application.Backtesting.QueueBacktests;
using EmTrading.Application.Backtesting.Strategies;

namespace EmTrading.Presentation;

/// <summary>
/// Strategy selection. The parameter form is generated from the selected strategy's descriptor,
/// so a new strategy needs no UI changes.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty] private IReadOnlyList<StrategyDescriptor> _strategies = [];
    [ObservableProperty] private StrategyDescriptor? _selectedStrategy;

    public ObservableCollection<StrategyParameterInput> StrategyParameters { get; } = [];

    [RelayCommand]
    private async Task LoadStrategiesAsync(CancellationToken cancellationToken)
    {
        var strategies = await _dispatcher.SendAsync(new GetStrategiesQuery(), cancellationToken);
        if (strategies.IsFailure)
        {
            StatusMessage = Describe(strategies.Error);
            return;
        }

        Strategies = strategies.Value;
        SelectedStrategy ??= Strategies.FirstOrDefault();
    }

    partial void OnSelectedStrategyChanged(StrategyDescriptor? value)
    {
        StrategyParameters.Clear();
        foreach (var parameter in value?.Parameters ?? [])
            StrategyParameters.Add(new StrategyParameterInput(parameter));
    }

    /// <summary>Runs the selected strategy for every symbol in the list as parallel background jobs.</summary>
    [RelayCommand]
    private async Task RunBacktestBatchAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDateRange(out var start, out var end) || !TryGetStrategy(out var strategy, out var parameters))
            return;

        var result = await _dispatcher.SendAsync(
            new QueueBacktestsCommand(strategy.Name, ParseSymbolList(), start, end, InitialCapital, parameters), cancellationToken);

        StatusMessage = result.Match(
            queued => queued.AlreadyQueued.Count == 0
                ? $"Queued {queued.JobIds.Count} backtest job(s)."
                : $"Queued {queued.JobIds.Count} backtest job(s); already running: {string.Join(", ", queued.AlreadyQueued)}.",
            Describe);
    }

    /// <summary>
    /// Selected strategy plus the form values. The form checks each value against its descriptor
    /// for quick feedback; the command validator checks them again.
    /// </summary>
    private bool TryGetStrategy([NotNullWhen(true)] out StrategyDescriptor? strategy, out IReadOnlyDictionary<string, string> parameters)
    {
        strategy = SelectedStrategy;
        parameters = StrategyParameters.ToDictionary(p => p.Name, p => p.Value);

        if (strategy is null)
        {
            StatusMessage = "Select a strategy.";
            return false;
        }

        var errors = strategy.Validate(parameters);
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return false;
        }

        return true;
    }
}

/// <summary>
/// One field of the generated strategy form.
/// </summary>
public sealed partial class StrategyParameterInput : ObservableObject
{
    private readonly StrategyParameterDescriptor _descriptor;

    public StrategyParameterInput(StrategyParameterDescriptor descriptor)
    {
        _descriptor = descriptor;
        _value = descriptor.FormatValue(descriptor.DefaultValue);
    }

    public string Name => _descriptor.Name;

    public string DisplayName => _descriptor.DisplayName;

    /// <summary>Description plus the allowed range, shown as a tooltip.</summary>
    public string Hint
    {
        get
        {
            var range = (_descriptor.Minimum, _descriptor.Maximum) switch
            {
                ({ } min, { } max) => $"{_descriptor.FormatValue(min)} – {_descriptor.FormatValue(max)}",
                ({ } min, null) => $"≥ {_descriptor.FormatValue(min)}",
                (null, { } max) => $"≤ {_descriptor.FormatValue(max)}",
                _ => null
            };
            return string.Join(" ", new[] { _descriptor.Description, range is null ? null : $"Zakres: {range}." }.Where(s => s is not null));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    private string _value;

    /// <summary>Validation message for this field alone, or <c>null</c>.</summary>
    public string? Error => _descriptor.TryParse(Value, out _, out var error) ? null : error;
}
