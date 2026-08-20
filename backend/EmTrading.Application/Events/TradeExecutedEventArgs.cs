namespace EmTrading.Application.Events;

public class TradeExecutedEventArgs : EventArgs
{
    public string Symbol { get; }
    public string Direction { get; } // np. "BUY", "SELL"
    public decimal Price { get; }
    public decimal Quantity { get; }
    public DateTime ExecutedAt { get; }

    public TradeExecutedEventArgs(string symbol, string direction, decimal price, decimal quantity, DateTime executedAt)
    {
        Symbol = symbol;
        Direction = direction;
        Price = price;
        Quantity = quantity;
        ExecutedAt = executedAt;
    }
}