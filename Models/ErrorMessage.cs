using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class ErrorMessage
{
    [JsonProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("DisplayArea")]
    public string DisplayArea { get; set; } = string.Empty;

    [JsonProperty("DisplayField")]
    public string DisplayField { get; set; } = string.Empty;

    [JsonProperty("GUID")]
    public string GUID { get; set; } = string.Empty;

    [JsonProperty("TargetField")]
    public string TargetField { get; set; } = string.Empty;

    [JsonProperty("Message")]
    public string Message { get; set; } = string.Empty;
}