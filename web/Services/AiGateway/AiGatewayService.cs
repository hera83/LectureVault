using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using web.Services.AiGateway.Dtos.Common;
using web.Services.AiGateway.Dtos.Errors;
using web.Services.AiGateway.Interfaces;

namespace web.Services.AiGateway;

public partial class AiGatewayService(
    IAiGatewayConfigurationProvider configurationProvider,
    AiGatewayHttpClientFactory httpClientFactory) : IAiGatewayService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var config = await configurationProvider.GetActiveConfigurationAsync(cancellationToken);
        return httpClientFactory.Create(config.BaseUrl, config.ApiKey, config.RequestTimeoutSeconds);
    }

    private static async Task<TResponse> GetAsync<TResponse>(HttpClient client, string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, new HttpRequestMessage(HttpMethod.Get, endpoint), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<TResponse> PostAsync<TRequest, TResponse>(HttpClient client, string endpoint, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendJsonAsync(client, HttpMethod.Post, endpoint, request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<TResponse> PutAsync<TRequest, TResponse>(HttpClient client, string endpoint, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendJsonAsync(client, HttpMethod.Put, endpoint, request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<TResponse> PostAsync<TResponse>(HttpClient client, string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, new HttpRequestMessage(HttpMethod.Post, endpoint), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonAsync<TResponse>(response, cancellationToken);
    }

    private static async Task PostNoContentAsync(HttpClient client, string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, new HttpRequestMessage(HttpMethod.Post, endpoint), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task<TResponse> DeleteAsync<TResponse>(HttpClient client, string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, new HttpRequestMessage(HttpMethod.Delete, endpoint), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonAsync<TResponse>(response, cancellationToken);
    }

    private static async Task DeleteAsync(HttpClient client, string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, new HttpRequestMessage(HttpMethod.Delete, endpoint), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task PostNoContentAsync<TRequest>(HttpClient client, string endpoint, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendJsonAsync(client, HttpMethod.Post, endpoint, request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static Task<HttpResponseMessage> SendJsonAsync<TRequest>(
        HttpClient client,
        HttpMethod method,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var message = new HttpRequestMessage(method, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return SendAsync(client, message, cancellationToken, completionOption);
    }

    private static Task<HttpResponseMessage> SendMultipartAsync(
        HttpClient client,
        HttpMethod method,
        string endpoint,
        MultipartFormDataContent content,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        var message = new HttpRequestMessage(method, endpoint) { Content = content };
        return SendAsync(client, message, cancellationToken, completionOption);
    }

    // Alle kald til gatewayen går herigennem, så netværksfejl og timeouts bliver til en
    // AiGatewayException med 502/504 i stedet for en anonym 500 fra GlobalExceptionHandler.
    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage message,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        try
        {
            return await client.SendAsync(message, completionOption, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiGatewayException(HttpStatusCode.BadGateway,
                $"Kunne ikke forbinde til AiGateway ({client.BaseAddress}): {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiGatewayException(HttpStatusCode.GatewayTimeout,
                $"AiGateway svarede ikke inden for {client.Timeout.TotalSeconds:0} sekunder ({message.RequestUri}). Timeout styres af AiGateway:RequestTimeoutSeconds.");
        }
    }

    private static void AddFilePart(MultipartFormDataContent content, string fieldName, Stream fileContent, string fileName, string? contentType)
    {
        var streamContent = new StreamContent(fileContent);
        // Browseres Blob.type (fx MediaRecorders "audio/webm;codecs=opus") har ofte parametre,
        // men AiGateway kaster en uhåndteret 500 hvis Content-Type på fil-delen har parametre -
        // send derfor kun det rene "type/subtype" videre.
        streamContent.Headers.ContentType = string.IsNullOrWhiteSpace(contentType)
            ? new MediaTypeHeaderValue("application/octet-stream")
            : new MediaTypeHeaderValue(MediaTypeHeaderValue.Parse(contentType).MediaType!);
        content.Add(streamContent, fieldName, fileName);
    }

    private static void AddFormValue(MultipartFormDataContent content, string fieldName, string? value)
    {
        if (value is not null)
        {
            content.Add(new StringContent(value), fieldName);
        }
    }

    private static async Task<AiGatewayFileResultDto> GetFileAsync(HttpClient client, string endpoint, CancellationToken cancellationToken)
    {
        var response = await SendAsync(client, new HttpRequestMessage(HttpMethod.Get, endpoint), cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ToFileResultAsync(response, cancellationToken);
    }

    private static async Task<AiGatewayFileResultDto> ToFileResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new AiGatewayFileResultDto
        {
            Content = stream,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            FileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName
        };
    }

    private static async Task<TResponse> ReadJsonAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return Deserialize<TResponse>(payload, response.RequestMessage?.RequestUri?.AbsolutePath);
    }

    // Passer svaret ikke til DTO'en (fx fordi gatewayen har ændret sit format), kastes en
    // AiGatewayException med 502, der nævner endpointet og typen, i stedet for en JsonException.
    private static TResponse Deserialize<TResponse>(string payload, string? endpoint)
    {
        try
        {
            return JsonSerializer.Deserialize<TResponse>(payload, JsonOptions)
                   ?? throw new JsonException("Svaret var tomt.");
        }
        catch (JsonException ex)
        {
            throw new AiGatewayException(HttpStatusCode.BadGateway,
                $"Uventet svar fra AiGateway på {endpoint}: kunne ikke læses som {typeof(TResponse).Name}. {ex.Message}");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        string message = $"AiGateway request fejlede med status {(int)response.StatusCode}.";
        string? traceId = null;
        IReadOnlyDictionary<string, string[]>? errors = null;

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var error = JsonSerializer.Deserialize<ErrorResponseDto>(raw, JsonOptions);
                if (error is not null)
                {
                    message = !string.IsNullOrWhiteSpace(error.Detail) ? error.Detail : error.Title ?? raw;
                    traceId = error.TraceId;
                    errors = error.Errors;
                }
                else
                {
                    message = raw;
                }
            }
            catch
            {
                message = raw;
            }
        }

        var endpoint = response.RequestMessage?.RequestUri?.AbsolutePath;

        // 401 og 403 handler om servicens egen nøgle (AiGateway:ApiKey), ikke om den bruger, der
        // er logget ind i appen. De må derfor ikke sendes uændret videre, så browseren tror, det
        // er brugerens login eller rettigheder, der er galt.
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new AiGatewayException(HttpStatusCode.BadGateway,
                $"AiGateway afviste servicens API-nøgle på {endpoint} (AiGateway:ApiKey mangler, er ugyldig eller udløbet). Gatewayens svar: {message}",
                traceId, errors, response.StatusCode),
            HttpStatusCode.Forbidden => new AiGatewayException(HttpStatusCode.Forbidden,
                $"AiGateway-nøglen har ikke adgang til {endpoint}. Funktionen kræver flere rettigheder i AiGateway, end nøglen i AiGateway:ApiKey har. Gatewayens svar: {message}",
                traceId, errors, response.StatusCode),
            _ => new AiGatewayException(response.StatusCode, message, traceId, errors, response.StatusCode)
        };
    }
}
