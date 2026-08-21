using Newtonsoft.Json;

namespace EmTrading.Domain.Lean;

public class LeanSeries
{
    [JsonProperty("values")]
    public List<List<double>> Values { get; set; }
}