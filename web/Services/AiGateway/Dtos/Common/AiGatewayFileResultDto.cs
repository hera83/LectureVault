namespace web.Services.AiGateway.Dtos.Common;

public class AiGatewayFileResultDto
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
}
