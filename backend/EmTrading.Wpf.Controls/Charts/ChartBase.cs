using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using ScottPlot.WPF;

namespace EmTrading.Wpf.Controls.Charts;

/// <summary>
/// Hosts ScottPlot plots and redraws them whenever a dependency property declared with
/// <see cref="RedrawOnChange"/> changes. Derived charts only describe what to draw.
/// </summary>
public abstract class ChartBase : ContentControl
{
    protected static readonly ScottPlot.Color Positive = ScottPlot.Color.FromHex("#2E7D32");
    protected static readonly ScottPlot.Color Negative = ScottPlot.Color.FromHex("#C62828");
    protected static readonly ScottPlot.Color Accent = ScottPlot.Color.FromHex("#1565C0");

    protected ChartBase()
    {
        Loaded += (_, _) => Redraw();
    }

    protected static PropertyMetadata RedrawOnChange(object? defaultValue = null) =>
        new(defaultValue, (d, _) => ((ChartBase)d).Redraw());

    protected static WpfPlot CreatePlot()
    {
        var plot = new WpfPlot();
        plot.Plot.FigureBackground.Color = ScottPlot.Colors.White;
        plot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#EEEEEE");
        return plot;
    }

    /// <summary>Clears and draws every plot; called on the UI thread.</summary>
    protected abstract void Render();

    private void Redraw()
    {
        if (!IsLoaded)
            return;

        Render();
    }

    protected static double[] ToOADates(IEnumerable<DateTime> times) => times.Select(t => t.ToOADate()).ToArray();

    protected static void PercentTicks(IYAxis axis) =>
        axis.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic { LabelFormatter = value => $"{value:0.#}%" };
}
