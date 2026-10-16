using System.ComponentModel.DataAnnotations;

namespace EmTrading.Infrastructure.Configuration;

/// <summary>
/// Keys are not stored in appsettings.json. Set them with:
/// <c>dotnet user-secrets set "Alpaca:ApiKey" "..." --project EmTrading.App</c>
/// </summary>
public sealed class AlpacaOptions
{
    public const string SectionName = "Alpaca";

    public string? ApiKey { get; set; }

    public string? ApiSecret { get; set; }

    [Required, Url]
    public string DataBaseUrl { get; set; } = "https://data.alpaca.markets";

    /// <summary>"iex" is available on the free plan, "sip" requires a subscription.</summary>
    [Required]
    public string Feed { get; set; } = "iex";

    public bool HasCredentials => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}
