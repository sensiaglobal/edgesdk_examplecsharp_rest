using Newtonsoft.Json;

namespace HCC2RestClient.Models;

public class RestTagMetaData
{
    [JsonProperty("dataType")]
    public string DataType { get; set; } = string.Empty;

    [JsonProperty("unit")]
    public string Unit { get; set; } = "NONE";

    [JsonProperty("min")]
    public string Min { get; set; } = "0";

    [JsonProperty("max")]
    public string Max { get; set; } = "9999999";

    [JsonProperty("noProtobuf")]
    public string NoProtobuf { get; set; } = "false";

    [JsonProperty("builtinEnums")]
    public string BuiltinEnums { get; set; } = "";

    [JsonProperty("isInput")]
    public string IsInput { get; set; } = "false";

    [JsonProperty("isOutput")]
    public string IsOutput { get; set; } = "false";

    [JsonProperty("arraySize")]
    public string ArraySize { get; set; } = "1";
}


