using web.Services.AiGateway.Interfaces;

namespace web.Services.AiGateway;

public class AiGatewayConfigurationProvider(IConfiguration configuration) : IAiGatewayConfigurationProvider
{
    public Task<AiGatewaySettings> GetActiveConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var settings = new AiGatewaySettings
        {
            BaseUrl = configuration["AiGateway:BaseUrl"] ?? "http://localhost:5000",
            ApiKey = configuration["AiGateway:ApiKey"],
            DefaultChatModel = configuration["AiGateway:DefaultChatModel"],
            DefaultLanguage = configuration["AiGateway:DefaultLanguage"],
            DefaultTtsModel = configuration["AiGateway:DefaultTtsModel"],
            DefaultTtsVoice = configuration["AiGateway:DefaultTtsVoice"],
            DefaultSttModel = configuration["AiGateway:DefaultSttModel"],
            RequestTimeoutSeconds = int.TryParse(configuration["AiGateway:RequestTimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : 300,
        };

        return Task.FromResult(settings);
    }
}
