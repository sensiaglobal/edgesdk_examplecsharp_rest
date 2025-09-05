using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class WebhookSubscriptionRequest
{
    [JsonProperty("callbackAPi")]
    public string CallbackApi { get; set; } = string.Empty;

    [JsonProperty("topics")]
    public List<string> Topics { get; set; } = new();

    [JsonProperty("includeOptional")]
    public bool IncludeOptional { get; set; }
}