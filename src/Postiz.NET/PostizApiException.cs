using System.Net;

namespace Postiz;

public sealed class PostizApiException : HttpRequestException
{
    public PostizApiException(
        HttpStatusCode statusCode,
        string? code,
        string? correlationId,
        string responseBody)
        : base($"Social Manager returned HTTP {(int)statusCode}.", null, statusCode)
    {
        StatusCode = statusCode;
        Code = code;
        CorrelationId = correlationId;
        ResponseBody = responseBody;
    }

    public new HttpStatusCode StatusCode { get; }

    public string? ErrorCode => Code;

    public bool IsTransient =>
        StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)StatusCode >= 500;

    public PostizApiReasonCode ReasonCode => Code switch
    {
        "media_video_required" => PostizApiReasonCode.MediaVideoRequired,
        "media_transcode_failed" => PostizApiReasonCode.MediaTranscodeFailed,
        "media_format_unsupported" => PostizApiReasonCode.MediaFormatUnsupported,
        "youtube_scope_insufficient" => PostizApiReasonCode.YoutubeScopeInsufficient,
        "thumbnail_rejected" => PostizApiReasonCode.ThumbnailRejected,
        "thumbnail_scope_missing" => PostizApiReasonCode.ThumbnailScopeMissing,
        "youtube_account_not_found" => PostizApiReasonCode.YoutubeAccountNotFound,
        "youtube_authentication_required" => PostizApiReasonCode.YoutubeAuthenticationRequired,
        "youtube_publish_failed" => PostizApiReasonCode.YoutubePublishFailed,
        _ => HttpReasonCode,
    };

    private PostizApiReasonCode HttpReasonCode => StatusCode switch
    {
        HttpStatusCode.BadRequest => PostizApiReasonCode.BadRequest,
        HttpStatusCode.Unauthorized => PostizApiReasonCode.Unauthorized,
        HttpStatusCode.Forbidden => PostizApiReasonCode.Forbidden,
        HttpStatusCode.NotFound => PostizApiReasonCode.NotFound,
        HttpStatusCode.Conflict => PostizApiReasonCode.Conflict,
        HttpStatusCode.TooManyRequests => PostizApiReasonCode.TooManyRequests,
        HttpStatusCode.BadGateway => PostizApiReasonCode.BadGateway,
        HttpStatusCode.ServiceUnavailable => PostizApiReasonCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout => PostizApiReasonCode.GatewayTimeout,
        _ when (int)StatusCode >= 500 => PostizApiReasonCode.ServerError,
        _ => PostizApiReasonCode.Unknown,
    };

    public string? Code { get; }

    public string? CorrelationId { get; }

    public string ResponseBody { get; }
}

public enum PostizApiReasonCode
{
    Unknown,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TooManyRequests,
    BadGateway,
    ServiceUnavailable,
    GatewayTimeout,
    ServerError,
    MediaVideoRequired,
    MediaTranscodeFailed,
    MediaFormatUnsupported,
    YoutubeScopeInsufficient,
    ThumbnailRejected,
    ThumbnailScopeMissing,
    YoutubeAccountNotFound,
    YoutubeAuthenticationRequired,
    YoutubePublishFailed,
}
