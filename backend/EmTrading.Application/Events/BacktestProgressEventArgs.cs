namespace EmTrading.Application.Events;

public class BacktestProgressEventArgs : EventArgs
{
    public int ProgressPercentage { get; }
    public string CurrentDate { get; }

    public BacktestProgressEventArgs(int progressPercentage, string currentDate)
    {
        ProgressPercentage = progressPercentage;
        CurrentDate = currentDate;
    }
}