using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class RestDataPoint
{
    [JsonProperty("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonProperty("defaultValue")]
    public string DefaultValue { get; set; } = "0";

    [JsonProperty("tagSubClass")]
    public string TagSubClass { get; set; } = "diagnostics";

    [JsonProperty("metadata")]
    public RestTagMetaData Metadata { get; set; } = new();

    [JsonProperty("unityUI")]
    public RestTagUnityUI UnityUI { get; set; } = new();
}