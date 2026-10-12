using System.Text.Json.Serialization;

namespace web.Services.AiGateway.Dtos.Speaches;

public class ListVoicesResponseDto
{
    [JsonPropertyName("voices")]
    public List<VoiceDto>? Voices { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }
}
