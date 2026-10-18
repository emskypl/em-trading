using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure.Alpaca;

/// <summary>
/// Keys are not stored in appsettings.json. Set them with:
/// <c>dotnet user-secrets set "Alpaca:ApiKey" "..." --project EmTrading.App</c>
/// </summary>
public sealed class AlpacaOptions
{
    public const string SectionName = "Alpaca";

    /// <summary>The only trading host allowed until live trading gets its own safeguards.</summary>
    public const string PaperTradingHost = "paper-api.alpaca.markets";

    public string? ApiKey { get; set; }

    public string? ApiSecret { get; set; }

    /// <summary>Keys of the paper trading account, if they differ from the market data keys.</summary>
    public string? TradingApiKey { get; set; }

    public string? TradingApiSecret { get; set; }

    [Required, Url]
    public string DataBaseUrl { get; set; } = "https://data.alpaca.markets";

    /// <summary>"iex" is available on the free plan, "sip" requires a subscription.</summary>
    [Required]
    public string Feed { get; set; } = "iex";

    /// <summary>Plan limit for API calls; 200 on the free plan.</summary>
    [Range(1, 10_000)]
    public int RequestsPerMinute { get; set; } = 200;

    [Required, Url]
    public string TradingBaseUrl { get; set; } = $"https://{PaperTradingHost}";

    /// <summary>Order and fill events of the trading account.</summary>
    [Required]
    public string TradingStreamUrl { get; set; } = $"wss://{PaperTradingHost}/stream";

    /// <summary>Live trades, quotes and minute bars; the feed is appended, e.g. <c>/iex</c>.</summary>
    [Required]
    public string MarketDataStreamUrl { get; set; } = "wss://stream.data.alpaca.markets/v2";

    /// <summary>How often working orders are compared with the broker, in case a stream event was missed.</summary>
    [Range(5, 3600)]
    public int ReconcileIntervalSeconds { get; set; } = 30;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);

    public bool HasTradingCredentials => !string.IsNullOrWhiteSpace(TradingKey) && !string.IsNullOrWhiteSpace(TradingSecret);

    public string? TradingKey => string.IsNullOrWhiteSpace(TradingApiKey) ? ApiKey : TradingApiKey;

    public string? TradingSecret => string.IsNullOrWhiteSpace(TradingApiSecret) ? ApiSecret : TradingApiSecret;
}

/// <summary>
/// Refuses to start with a trading endpoint other than paper trading: real-money trading is out of scope until it has
/// its own explicit switch, confirmations and limits.
/// </summary>
internal sealed class PaperTradingOnlyValidation : IValidateOptions<AlpacaOptions>
{
    public ValidateOptionsResult Validate(string? name, AlpacaOptions options)
    {
        var failures = new List<string>();
        foreach (var (setting, url) in new[] { ("TradingBaseUrl", options.TradingBaseUrl), ("TradingStreamUrl", options.TradingStreamUrl) })
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.Equals(uri.Host, AlpacaOptions.PaperTradingHost, StringComparison.OrdinalIgnoreCase))
                failures.Add($"Alpaca:{setting} must point to {AlpacaOptions.PaperTradingHost}; only paper trading is supported.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
