using Newtonsoft.Json;

namespace EmTrading.Domain.Lean
{
    public class LeanResult
    {
        [JsonProperty("statistics")]
        public Dictionary<string, string> Statistics { get; set; }

        [JsonProperty("charts")]
        public Dictionary<string, LeanChart> Charts { get; set; }
    }
}