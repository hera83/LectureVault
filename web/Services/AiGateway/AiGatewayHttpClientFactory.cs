namespace web.Services.AiGateway;

public class AiGatewayHttpClientFactory(IHttpClientFactory httpClientFactory)
{
    public HttpClient Create(string baseUrl, string? apiKey, int requestTimeoutSeconds)
    {
        var client = httpClientFactory.CreateClient("AiGateway");
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 300);

        client.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }
}
