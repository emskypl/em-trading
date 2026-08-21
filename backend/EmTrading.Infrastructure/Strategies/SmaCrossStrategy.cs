using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace EmTrading.Infrastructure.Strategies;

public class SmaCrossStrategy : QCAlgorithm
{
    private Symbol _symbol;
    
    // Wskaźniki 15m
    private ExponentialMovingAverage _emaFast;
    private ExponentialMovingAverage _emaSlow;
    private AverageDirectionalIndex _adx;
    private AverageTrueRange _atr;
    
    // Pamięć danych dziennych
    private RollingWindow<decimal> _dailyCloses;
    
    // Zmienne do Trailing Stop
    private decimal _stopLossPrice;
    private bool _isLong;

    public override void Initialize()
    {
        SetStartDate(2025, 1, 4);
        SetEndDate(2026, 12, 14);
        SetCash(1000000);
        // Read symbol passed into the algorithm parameters (fallback to AAPL)
        var symbolParam = GetParameter("symbol") ?? "AAPL";
        var ticker = symbolParam.Trim().ToUpperInvariant();

        // Subscribe to the requested equity FIRST
        _symbol = AddEquity(ticker, Resolution.Minute).Symbol;
        // Now it's safe to set holdings for that Symbol
        SetHoldings(_symbol, 0.85);

        // RollingWindow for 200 daily closes
        _dailyCloses = new RollingWindow<decimal>(201);
        var dailyConsolidator = new TradeBarConsolidator(TimeSpan.FromDays(1));
        dailyConsolidator.DataConsolidated += (s, bar) => _dailyCloses.Add(bar.Close);
        SubscriptionManager.AddConsolidator(_symbol, dailyConsolidator);

        // 15-minute consolidator (use the period you actually want)
        var cons15m = new TradeBarConsolidator(TimeSpan.FromMinutes(15));

        _emaFast = new ExponentialMovingAverage(26);
        _emaSlow = new ExponentialMovingAverage(130);
        _adx = new AverageDirectionalIndex(14);
        _atr = new AverageTrueRange(14);
        SetWarmUp(TimeSpan.FromDays(30)); 

        RegisterIndicator(_symbol, _emaFast, cons15m);
        RegisterIndicator(_symbol, _emaSlow, cons15m);
        RegisterIndicator(_symbol, _adx, cons15m);
        RegisterIndicator(_symbol, _atr, cons15m);

        cons15m.DataConsolidated += On15mBar;
        SubscriptionManager.AddConsolidator(_symbol, cons15m);

        SetWarmUp(201, Resolution.Daily);
    }

    private void On15mBar(object sender, TradeBar bar)
    {
        if (IsWarmingUp || !_dailyCloses.IsReady || !_adx.IsReady) return;

        var price = bar.Close;
        var close100 = _dailyCloses[100];
        var close200 = _dailyCloses[200];
        
        bool dailyLongFilter = price > close100 && price > close200;
        bool dailyShortFilter = price < close100 && price < close200;
        
        bool adxFilter = _adx.Current.Value > 24m;
        
        if (!Portfolio.Invested)
        {
            if (dailyLongFilter && adxFilter && _emaFast.Current.Value > _emaSlow.Current.Value)
            {
                ExecuteTrade(OrderDirection.Buy, price);
            }
            else if (dailyShortFilter && adxFilter && _emaFast.Current.Value < _emaSlow.Current.Value)
            {
                ExecuteTrade(OrderDirection.Sell, price);
            }
        }
        else
        {
            UpdateTrailingStop(price);
        }
    }

    private void ExecuteTrade(OrderDirection direction, decimal currentPrice)
    {
        decimal riskPerTrade = Portfolio.TotalPortfolioValue * 0.01m;
        decimal stopDistance = _atr.Current.Value * 1.5m;
        
        decimal positionSizeDecimal = riskPerTrade / stopDistance;
        int shares = (int)Math.Floor(positionSizeDecimal);
        
        if (shares == 0) return;

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
        decimal stopDistance = _atr.Current.Value * 1.5m;

        if (_isLong)
        {
            decimal newStop = currentPrice - stopDistance;
            if (newStop > _stopLossPrice) _stopLossPrice = newStop; // Przesuń SL wyżej
            
            if (currentPrice <= _stopLossPrice) Liquidate(_symbol, "Trailing Stop Long");
        }
        else
        {
            decimal newStop = currentPrice + stopDistance;
            if (newStop < _stopLossPrice) _stopLossPrice = newStop; // Przesuń SL niżej
            
            if (currentPrice >= _stopLossPrice) Liquidate(_symbol, "Trailing Stop Short");
        }
    }
    
    public override void OnData(Slice data)
    {
        if (IsWarmingUp) return;
        
        if (!data.Bars.ContainsKey(_symbol) || !_emaFast.IsReady || !_emaSlow.IsReady) return;

        // Logika przecięcia średnich
        if (_emaFast > _emaSlow)
        {
            if (!Portfolio[_symbol].Invested)
            {
                SetHoldings(_symbol, 0.80m);
            }
        }
        else if (_emaFast < _emaSlow)
        {
            if (Portfolio[_symbol].Invested)
            {
                Liquidate(_symbol);
            }
        }
    }

    public override void OnEndOfAlgorithm()
    {
        Liquidate(); 
    }
}