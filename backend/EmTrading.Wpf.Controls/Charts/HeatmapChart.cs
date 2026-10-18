using System.Globalization;
using System.Windows;
using ScottPlot;
using ScottPlot.WPF;

namespace EmTrading.Wpf.Controls.Charts;

/// <summary>
/// Values over two categorical axes as coloured cells with the value printed inside, plus a colour bar.
/// One cell can be outlined, e.g. the best parameter combination.
/// </summary>
public sealed class HeatmapChart : ChartBase
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(HeatmapData), typeof(HeatmapChart), RedrawOnChange());

    private readonly WpfPlot _plot = CreatePlot();

    public HeatmapChart()
    {
        Content = _plot;
    }

    public HeatmapData? Data
    {
        get => (HeatmapData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override void Render()
    {
        var plot = _plot.Plot;
        plot.Clear();
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();

        if (Data is { } data && data.Values.Length > 0)
            Draw(plot, data);

        _plot.Refresh();
    }

    internal static void Draw(Plot plot, HeatmapData data)
    {
        var rows = data.Values.GetLength(0);
        var columns = data.Values.GetLength(1);

        var intensities = new double[rows, columns];
        double? min = null, max = null;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var value = data.Values[row, column];
                intensities[row, column] = value ?? double.NaN;
                if (value is { } v)
                {
                    min = Math.Min(min ?? v, v);
                    max = Math.Max(max ?? v, v);
                }
            }
        }

        // ScottPlot centres cell (row, column) on (column, row), so ticks and the highlight use plain indexes.
        // It draws row 0 at the top; flipped, row 0 is at the bottom like on any y axis.
        var heatmap = plot.Add.Heatmap(intensities);
        heatmap.Colormap = new ScottPlot.Colormaps.Viridis();
        heatmap.NaNCellColor = ScottPlot.Color.FromHex("#F2F2F2");
        heatmap.FlipVertically = true;
        if (min is { } low && max is { } high && high > low)
            heatmap.ManualRange = new ScottPlot.Range(low, high);

        plot.Add.ColorBar(heatmap);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (data.Values[row, column] is not { } value)
                    continue;

                var text = plot.Add.Text(value.ToString(data.ValueFormat, CultureInfo.CurrentCulture), column, row);
                text.Alignment = Alignment.MiddleCenter;
                text.LabelFontSize = 11;
                text.LabelFontColor = IsBright(value, min ?? value, max ?? value) ? Colors.Black : Colors.White;
            }
        }

        if (data.Highlight is { } best)
        {
            var outline = plot.Add.Rectangle(best.Column - 0.5, best.Column + 0.5, best.Row - 0.5, best.Row + 0.5);
            outline.FillColor = Colors.Transparent;
            outline.LineColor = Negative;
            outline.LineWidth = 3;
        }

        plot.Axes.Bottom.SetTicks(Enumerable.Range(0, columns).Select(i => (double)i).ToArray(), data.XTicks.Take(columns).ToArray());
        plot.Axes.Left.SetTicks(Enumerable.Range(0, rows).Select(i => (double)i).ToArray(), data.YTicks.Take(rows).ToArray());
        plot.XLabel(data.XLabel);
        plot.YLabel(data.YLabel);
        plot.Axes.SetLimits(-0.5, columns - 0.5, -0.5, rows - 0.5);
    }

    /// <summary>Viridis is light at the top of its range: dark text there, white text elsewhere.</summary>
    private static bool IsBright(double value, double min, double max) => max > min && (value - min) / (max - min) > 0.6;
}
