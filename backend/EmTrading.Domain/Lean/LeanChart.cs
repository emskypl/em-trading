using Newtonsoft.Json;

namespace EmTrading.Domain.Lean;

public class LeanChart
{
    [JsonProperty("series")]
    public Dictionary<string, LeanSeries> Series { get; set; }
}