using QuantConnect;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;

namespace EmTrading.Infrastructure.Engine;

public class WpfResultHandler : BacktestingResultHandler
{
    public event Action<decimal>? OnEquityUpdated;

    // public override void Sample(string chartName, string seriesName, int seriesIndex, SeriesType seriesType, ISeriesPoint point)
    // {
    //     base.Sample(chartName, seriesName, seriesIndex, seriesType, new ChartPoint());
    //
    //     // Prwywyłanie aktualizacji equity w czasie rzeczywistym do aplikacji
    //     if (chartName == "Strategy Equity" && seriesName == "Equity")
    //     {
    //         OnEquityUpdated?.Invoke(value);
    //     }
    // }
}
