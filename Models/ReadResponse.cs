using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace HCC2RestClient.Models;

public class ReadResponse
{
    [JsonProperty("topic")]
    public string? Topic { get; set; } = string.Empty;

    [JsonProperty("value")]
    public object? Value { get; set; } = default;
}