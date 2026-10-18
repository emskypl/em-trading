using EmTrading.Wpf.Controls.Charts;
using SkiaSharp;

namespace EmTrading.Presentation.Tests;

/// <summary>
/// Renders charts off screen and checks pixels, for drawing rules that are easy to get backwards.
/// </summary>
public class ChartTests
{
    [Fact]
    public void Heatmap_draws_row_zero_at_the_bottom()
    {
        // One column, two rows: the low value in row 0, the high value in row 1.
        var data = new HeatmapData("x", "y", ["a"], ["low", "high"], new double?[,] { { 0 }, { 10 } });
        var plot = new ScottPlot.Plot();
        HeatmapChart.Draw(plot, data);
        plot.Layout.Frameless();
        plot.HideGrid();

        using var bitmap = SKBitmap.Decode(plot.GetImage(200, 200).GetImageBytes());
        var bottom = Brightness(bitmap.GetPixel(100, 175));
        var top = Brightness(bitmap.GetPixel(100, 25));

        // Viridis runs from dark purple (low) to yellow (high).
        Assert.True(top > bottom, $"Expected the high value at the top: top {top}, bottom {bottom}.");
    }

    private static int Brightness(SKColor color) => color.Red + color.Green + color.Blue;
}
