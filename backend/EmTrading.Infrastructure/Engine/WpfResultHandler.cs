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

// public class WpfResultHandler : BacktestingResultHandler
// {
//     // Delegat lub event, do którego podepniesz swój ViewModel
//     public Action<Dictionary<string, string>, Dictionary<DateTime, decimal>> OnResultReady;
//
//     public override void SendFinalResult(BacktestResultPacket packet)
//     {
//         base.SendFinalResult(packet);
//
//         if (packet.Results == null) return;
//
//         // 1. Wyciąganie statystyk (np. Net Profit, Win Rate, Drawdown)
//         var statistics = packet.Results.Statistics;
//
//         // 2. Wyciąganie punktów wykresu kapitału (Equity Curve)
//         var equityPoints = new Dictionary<DateTime, decimal>();
//         if (packet.Results.Charts.TryGetValue("Strategy Equity", out var equityChart) &&
//             equityChart.Series.TryGetValue("Equity", out var equitySeries))
//         {
//             foreach (var point in equitySeries.Values)
//             {
//                 // Lean zwraca czas w formacie unix timestamp, trzeba go przekonwertować
//                 var time = Time.UnixTimeStampToDateTime(point.x);
//                 equityPoints[time] = point.y;
//             }
//         }
//
//         // 3. Przekazanie danych do UI (ViewModelu)
//         OnResultReady?.Invoke(statistics, equityPoints);
//     }
// }