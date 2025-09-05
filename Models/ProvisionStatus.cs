using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class ProvisionStatus
{
    [JsonProperty("hasNewConfig")]
    public bool HasNewConfig { get; set; }
}