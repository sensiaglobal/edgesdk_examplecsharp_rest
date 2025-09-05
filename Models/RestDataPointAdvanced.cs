using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class RestDataPointAdvanced
{
    [JsonProperty("dataPointName")]
    public string DataPointName { get; set; } = string.Empty;

    [JsonProperty("quality")]
    public int Quality { get; set; }

    [JsonProperty("timeStamps")]
    public List<string> TimeStamps { get; set; } = new();

    [JsonProperty("values")]
    public List<object> Values { get; set; } = new();
}