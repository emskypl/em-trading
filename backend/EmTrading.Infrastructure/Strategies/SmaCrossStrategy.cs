// EmTrading.Infrastructure/Strategies/SmaCrossStrategy.cs

using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Indicators;

public class SmaCrossStrategy : QCAlgorithm
{
    private SimpleMovingAverage _fastSma;
    private SimpleMovingAverage _slowSma;

    public override void Initialize()
    {
        SetStartDate(2023, 1, 1);
        SetCash(100000);
        AddEquity("AAPL", Resolution.Daily);
        SetBenchmark(datetime => 100);
        _fastSma = SMA("AAPL", 10, Resolution.Daily);
        _slowSma = SMA("AAPL", 50, Resolution.Daily);
    }

    public override void OnData(Slice data)
    {
        if (!_slowSma.IsReady) return;

        if (_fastSma > _slowSma && !Portfolio["AAPL"].Invested)
        {
            SetHoldings("AAPL", 1.0);
        }
        else if (_fastSma < _slowSma && Portfolio["AAPL"].Invested)
        {
            Liquidate("AAPL");
        }
    }
}