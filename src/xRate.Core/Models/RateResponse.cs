using System.Text.Json.Serialization;
using xRate.Core.Services;

namespace xRate.Core.Models;

public class RateResponse
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("quote")]
    public string Quote { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public double Rate { get; set; }
}

[JsonSerializable(typeof(RateResponse[]))]
[JsonSerializable(typeof(GlobalCache))]
public partial class CurrencyContext : JsonSerializerContext { }