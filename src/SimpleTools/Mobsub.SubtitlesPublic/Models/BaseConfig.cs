using System.Text.Json.Serialization;

namespace Mobsub.SubtitlesPublic.Models;

public class BaseConfig
{
    public string PrivateRepoPath { get; set; }
    public string PublicRepoPath { get; set; }
    public string TempUploadPath { get; set; }
    public string ListAssFontsBinaryPath { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(BaseConfig))]
public partial class BaseConfigContext : JsonSerializerContext
{
}