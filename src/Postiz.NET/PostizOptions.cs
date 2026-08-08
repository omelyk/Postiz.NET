namespace Postiz;

public sealed class PostizOptions
{
    public required Uri BaseAddress { get; set; }

    public required string ApiKey { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

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

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ArgumentException("A Postiz API key is required.", nameof(ApiKey));
        }

        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        }

        if (MaxRetryAttempts is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetryAttempts));
        }
    }
}
