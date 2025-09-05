using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class DataPointItemsResult
{
    [JsonProperty("Result")]
    public string? Result { get; set; }

    [JsonProperty("Guid")]
    public string? Guid { get; set; }

    [JsonProperty("FullDataPointName")]
    public string? FullDataPointName { get; set; }

    [JsonProperty("Messages")]
    public List<ErrorMessage> Messages { get; set; } = new List<ErrorMessage>();
}
