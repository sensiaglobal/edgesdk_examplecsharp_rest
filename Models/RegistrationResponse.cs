using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class RegistrationResponse
{
    [JsonProperty("msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}