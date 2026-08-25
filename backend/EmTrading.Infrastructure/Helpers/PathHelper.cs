namespace EmTrading.Infrastructure.Helpers;

public static class PathHelper
{
    public static string GetSharedDataFolderPath()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        // Walk up until finding the .sln file directory
        while (currentDir != null && !currentDir.GetFiles("*.slnx").Any())
        {
            currentDir = currentDir.Parent;
        }

        // If found, place TradingData at the solution root level
        if (currentDir != null)
        {
            return Path.Combine(currentDir.FullName, "TradingData");
        }

        // Fallback if running from a deployed/installed release folder
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TradingData");
    }
    
    public static string GetResultsFolderPath()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        // Walk up until finding the .sln file directory
        while (currentDir != null && !currentDir.GetFiles("*.slnx").Any())
        {
            currentDir = currentDir.Parent;
        }

        // If found, place TradingData at the solution root level
        if (currentDir != null)
        {
            return Path.Combine(currentDir.FullName, "Results");
        }

        // Fallback if running from a deployed/installed release folder
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
    }
}