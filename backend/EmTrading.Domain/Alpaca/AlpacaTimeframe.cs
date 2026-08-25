using System.ComponentModel;

namespace EmTrading.Domain.Alpaca;

public enum AlpacaTimeframe
{
    [Description("1Min")] Minute1,
    [Description("5Min")] Minute5,
    [Description("15Min")] Minute15,
    [Description("45Min")] Minute45,
    
    [Description("1Hour")] Hour1,
    [Description("2Hour")] Hour2,
    [Description("4Hour")] Hour4,
    [Description("12Hour")] Hour12,
    
    [Description("1Day")] Day1,
    
    [Description("1Week")] Week1,
    
    [Description("1Month")] Month1,
    [Description("3Month")] Month3,
    [Description("12Month")] Month12
}