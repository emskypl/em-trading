namespace EmTrading.Infrastructure.Services;

public static class LeanDataFolderInitializer
{
    private static readonly Dictionary<string, string[]> DetailedStructure = new()
    {
        ["equity"] = new[] 
        { 
            @"usa\daily", 
            @"usa\hour", 
            @"usa\minute", 
            @"usa\second", 
            @"usa\tick", 
            @"usa\fundamental", 
            @"usa\shortable", 
            @"usa\map_files", 
            @"usa\factor_files" 
        },
        ["crypto"] = new[] 
        { 
            @"binance\daily", 
            @"binance\hour", 
            @"binance\minute", 
            @"coinbase\daily", 
            @"coinbase\hour", 
            @"coinbase\minute" 
        },
        ["forex"] = new[] 
        { 
            @"oanda\daily", 
            @"oanda\hour", 
            @"oanda\minute", 
            @"fxcm\daily", 
            @"fxcm\hour", 
            @"fxcm\minute" 
        },
        ["cfd"] = new[] { @"oanda\daily", @"oanda\hour", @"oanda\minute" },
        ["future"] = new[] { @"usa\daily", @"usa\minute", @"usa\margins" },
        ["option"] = new[] { @"usa\daily", @"usa\minute" },
        ["index"] = new[] { @"usa\daily", @"usa\minute" },
        ["alternative"] = Array.Empty<string>(),
        ["cryptofuture"] = Array.Empty<string>(),
        ["futureoption"] = Array.Empty<string>(),
        ["indexoption"] = Array.Empty<string>(),
        ["market-hours"] = Array.Empty<string>(),
        ["symbol-properties"] = Array.Empty<string>()
    };

    public static void Initialize(string baseDataPath)
    {
        if (string.IsNullOrWhiteSpace(baseDataPath)) return;

        Directory.CreateDirectory(baseDataPath);

        foreach (var (rootFolder, subFolders) in DetailedStructure)
        {
            var categoryPath = Path.Combine(baseDataPath, rootFolder);
            Directory.CreateDirectory(categoryPath);

            foreach (var subFolder in subFolders)
            {
                var fullSubFolderPath = Path.Combine(categoryPath, subFolder);
                Directory.CreateDirectory(fullSubFolderPath);
            }
        }
    }
}