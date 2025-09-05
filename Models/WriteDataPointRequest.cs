using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class WriteDataPointRequest
{
    [JsonProperty("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonProperty("value")]
    public object Value { get; set; } = new();

    [JsonProperty("msgSource")]
    public string MsgSource { get; set; } = string.Empty;

    [JsonProperty("quality")]
    public Quality Quality { get; set; } = Quality.Good;

    [JsonProperty("timeStamp")]
    public string TimeStamp { get; set; } = string.Empty;
}