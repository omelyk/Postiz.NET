namespace Postiz;

public sealed class PostizOptions
{
    public required Uri BaseAddress { get; set; }

    public string? ApiKey { get; set; }

    public string? OrganizationId { get; set; }

    public string? InternalClientId { get; set; }

    public string? InternalClientSecret { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan YoutubePublishTimeout { get; set; } = TimeSpan.FromMinutes(10);

    public int MaxRetryAttempts { get; set; } = 3;

    public Func<string?>? CorrelationIdFactory { get; set; }

    public void Validate()
    {
        if (!BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("Postiz BaseAddress must be absolute.", nameof(BaseAddress));
        }

        if (BaseAddress.Scheme != Uri.UriSchemeHttps && !BaseAddress.IsLoopback)
        {
            throw new ArgumentException("Postiz BaseAddress must use HTTPS outside local development.", nameof(BaseAddress));
        }

        var hasApiKey = !string.IsNullOrWhiteSpace(ApiKey);
        var hasInternalCredentials =
            !string.IsNullOrWhiteSpace(InternalClientId) &&
            !string.IsNullOrWhiteSpace(InternalClientSecret);
        if (!hasApiKey && !hasInternalCredentials)
        {
            throw new ArgumentException("A Postiz API key or internal client credentials are required.");
        }

        if (string.IsNullOrWhiteSpace(InternalClientId) != string.IsNullOrWhiteSpace(InternalClientSecret))
        {
            throw new ArgumentException("InternalClientId and InternalClientSecret must be configured together.");
        }

        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        }

        if (YoutubePublishTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(YoutubePublishTimeout));
        }

        if (MaxRetryAttempts is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetryAttempts));
        }
    }
}
