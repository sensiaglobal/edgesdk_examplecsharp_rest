using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class WebhookMessage
{
    [JsonProperty("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonProperty("value")]
    public object? Value { get; set; }
}