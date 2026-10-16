using System.Globalization;
using EmTrading.Application.Backtesting;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace EmTrading.Infrastructure.Strategies;

/// <summary>
/// Trend following on 15-minute bars: EMA fast/slow cross, filtered by ADX and by the price relative to the
/// 100 and 200 day closes. Positions are sized for 1% risk and exited by an ATR trailing stop.
/// </summary>
/// <remarks>
/// Parameters (see <see cref="StrategyParameterNames"/>): symbol, start-date, end-date, cash, fast-period, slow-period.
/// The daily filter needs 200 trading days of data before the start date for warm-up.
/// </remarks>
public class SmaCrossStrategy : QCAlgorithm
{
    private const int DailyLookback = 200;
    private const decimal AdxThreshold = 24m;
    private const decimal RiskPerTrade = 0.01m;
    private const decimal AtrStopMultiplier = 1.5m;

    private Symbol _symbol = null!;

    // 15-minute indicators
    private ExponentialMovingAverage _emaFast = null!;
    private ExponentialMovingAverage _emaSlow = null!;
    private AverageDirectionalIndex _adx = null!;
    private AverageTrueRange _atr = null!;

    // Daily closes for the trend filter
    private RollingWindow<decimal> _dailyCloses = null!;

    // Trailing stop state
    private decimal _stopLossPrice;
    private bool _isLong;

    public override void Initialize()
    {
        SetStartDate(GetDateParameter(StrategyParameterNames.StartDate, new DateTime(2025, 1, 1)));
        SetEndDate(GetDateParameter(StrategyParameterNames.EndDate, DateTime.Today));
        SetCash(GetParameter(StrategyParameterNames.Cash, 1_000_000m));

        var ticker = GetParameter(StrategyParameterNames.Symbol, "AAPL").Trim().ToUpperInvariant();
        _symbol = AddEquity(ticker, Resolution.Minute).Symbol;

        _dailyCloses = new RollingWindow<decimal>(DailyLookback + 1);
        var dailyConsolidator = new TradeBarConsolidator(TimeSpan.FromDays(1));
        dailyConsolidator.DataConsolidated += (_, bar) => _dailyCloses.Add(bar.Close);
        SubscriptionManager.AddConsolidator(_symbol, dailyConsolidator);

        var consolidator15M = new TradeBarConsolidator(TimeSpan.FromMinutes(15));
        _emaFast = new ExponentialMovingAverage(GetParameter(StrategyParameterNames.FastPeriod, 26));
        _emaSlow = new ExponentialMovingAverage(GetParameter(StrategyParameterNames.SlowPeriod, 130));
        _adx = new AverageDirectionalIndex(14);
        _atr = new AverageTrueRange(14);

        RegisterIndicator(_symbol, _emaFast, consolidator15M);
        RegisterIndicator(_symbol, _emaSlow, consolidator15M);
        RegisterIndicator(_symbol, _adx, consolidator15M);
        RegisterIndicator(_symbol, _atr, consolidator15M);

        consolidator15M.DataConsolidated += On15MinuteBar;
        SubscriptionManager.AddConsolidator(_symbol, consolidator15M);

        SetWarmUp(DailyLookback + 1, Resolution.Daily);
    }

    private void On15MinuteBar(object? sender, TradeBar bar)
    {
        if (IsWarmingUp || !_dailyCloses.IsReady || !_adx.IsReady || !_emaSlow.IsReady)
            return;

        var price = bar.Close;
        var close100 = _dailyCloses[100];
        var close200 = _dailyCloses[DailyLookback];

        var dailyLongFilter = price > close100 && price > close200;
        var dailyShortFilter = price < close100 && price < close200;
        var adxFilter = _adx.Current.Value > AdxThreshold;

        if (!Portfolio.Invested)
        {
            if (dailyLongFilter && adxFilter && _emaFast.Current.Value > _emaSlow.Current.Value)
                EnterPosition(OrderDirection.Buy, price);
            else if (dailyShortFilter && adxFilter && _emaFast.Current.Value < _emaSlow.Current.Value)
                EnterPosition(OrderDirection.Sell, price);
        }
        else
        {
            UpdateTrailingStop(price);
        }
    }

    private void EnterPosition(OrderDirection direction, decimal currentPrice)
    {
        var stopDistance = _atr.Current.Value * AtrStopMultiplier;
        if (stopDistance <= 0)
            return;

        var riskAmount = Portfolio.TotalPortfolioValue * RiskPerTrade;
        var shares = (int)Math.Floor(riskAmount / stopDistance);
        if (shares == 0)
            return;

        if (direction == OrderDirection.Buy)
        {
            MarketOrder(_symbol, shares);
            _stopLossPrice = currentPrice - stopDistance;
            _isLong = true;
        }
        else
        {
            MarketOrder(_symbol, -shares);
            _stopLossPrice = currentPrice + stopDistance;
            _isLong = false;
        }
    }

    private void UpdateTrailingStop(decimal currentPrice)
    {
        var stopDistance = _atr.Current.Value * AtrStopMultiplier;

        if (_isLong)
        {
            _stopLossPrice = Math.Max(_stopLossPrice, currentPrice - stopDistance);
            if (currentPrice <= _stopLossPrice)
                Liquidate(symbol: _symbol, tag: "Trailing Stop Long");
        }
        else
        {
            _stopLossPrice = Math.Min(_stopLossPrice, currentPrice + stopDistance);
            if (currentPrice >= _stopLossPrice)
                Liquidate(symbol: _symbol, tag: "Trailing Stop Short");
        }
    }

    public override void OnEndOfAlgorithm()
    {
        Liquidate();
    }

    private DateTime GetDateParameter(string name, DateTime defaultValue)
    {
        var value = GetParameter(name);
        return DateTime.TryParseExact(value, StrategyParameterNames.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : defaultValue;
    }
}
