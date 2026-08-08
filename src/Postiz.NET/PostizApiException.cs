using System.Net;

namespace Postiz;

public sealed class PostizApiException : HttpRequestException
{
    public PostizApiException(
        HttpStatusCode statusCode,
        string? code,
        string? correlationId,
        string responseBody)
        : base($"Postiz returned HTTP {(int)statusCode} ({statusCode}).", null, statusCode)
    {
        Code = code;
        CorrelationId = correlationId;
        ResponseBody = responseBody;
    }

    public string? Code { get; }

    public string? CorrelationId { get; }

    public string ResponseBody { get; }
}
