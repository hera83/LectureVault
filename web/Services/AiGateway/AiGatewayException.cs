using System.Net;

namespace web.Services.AiGateway;

public class AiGatewayException : Exception
{
    /// <summary>Statuskoden, appen svarer med, hvis fejlen ikke fanges (se GlobalExceptionHandler).</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Statuskoden fra AiGateway selv, eller null hvis gatewayen ikke kunne nås eller svaret ikke kunne læses.</summary>
    public HttpStatusCode? UpstreamStatusCode { get; }

    public string? TraceId { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public AiGatewayException(
        HttpStatusCode statusCode,
        string message,
        string? traceId = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        HttpStatusCode? upstreamStatusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        UpstreamStatusCode = upstreamStatusCode;
        TraceId = traceId;
        Errors = errors;
    }
}
