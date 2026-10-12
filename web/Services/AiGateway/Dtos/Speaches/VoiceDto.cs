using System.Text.Json.Serialization;

namespace web.Services.AiGateway.Dtos.Speaches;

public class VoiceDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
}
