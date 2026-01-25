using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mobsub.Helper.ZhConvert;

public class FanhuajiOptions
{
    [JsonPropertyName("converter")]
    public string Converter { get; set; } = "Taiwan";

    [JsonPropertyName("ignoreTextPatterns")]
    public List<string> IgnoreTextPatterns { get; set; } = [];

    [JsonPropertyName("userPostReplace")]
    public string? UserPostReplace { get; set; }

    [JsonPropertyName("userPreReplace")]
    public string? UserPreReplace { get; set; }

    [JsonPropertyName("userProtect")]
    public string? UserProtect { get; set; }

    [JsonPropertyName("jpTextStyles")]
    public string? JpTextStyles { get; set; }

    [JsonPropertyName("jpStrategy")]
    public string? JpStrategy { get; set; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }
}

public class FanhuajiResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public FanhuajiData? Data { get; set; }
}

public class FanhuajiData
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("diff")]
    public object? Diff { get; set; }

    [JsonPropertyName("usedModules")]
    public object? UsedModules { get; set; }
}

[JsonSerializable(typeof(FanhuajiOptions))]
[JsonSerializable(typeof(FanhuajiResponse))]
public partial class FanhuajiJsonContext : JsonSerializerContext
{
}
