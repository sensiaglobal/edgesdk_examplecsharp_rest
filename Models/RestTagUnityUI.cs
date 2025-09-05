using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class RestTagUnityUI
{
    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("shortDisplayName")]
    public string ShortDisplayName { get; set; } = string.Empty;

    [JsonProperty("displayMin")]
    public string DisplayMin { get; set; } = "10";

    [JsonProperty("displayMax")]
    public string DisplayMax { get; set; } = "40";

    [JsonProperty("uiSize")]
    public string UiSize { get; set; } = "2";

    [JsonProperty("configGroup")]
    public string ConfigGroup { get; set; } = "Grouped Rest Items";

    [JsonProperty("configSection")]
    public string ConfigSection { get; set; } = "Rest Tags";
}