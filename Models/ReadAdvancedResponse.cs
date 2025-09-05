using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class ReadAdvancedResponse
{
    [JsonProperty("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonProperty("msgSource")]
    public string MsgSource { get; set; } = string.Empty;

    [JsonProperty("datapoints")]
    public List<RestDataPointAdvanced> Datapoints { get; set; } = new();
}
