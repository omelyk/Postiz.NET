using System.Net;
using System.Text;
using System.Text.Json;

namespace Postiz.Transport;

internal sealed class PostizTransport(HttpClient httpClient, PostizOptions options)
{
    internal Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, path, null, retryable: true, cancellationToken);

    internal Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Post, path, Json(body), retryable: false, cancellationToken);

    internal Task<T> GetPublicAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, path, null, retryable: true, cancellationToken, AuthenticationMode.None);

    internal Task<T> GetInternalAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, path, null, retryable: true, cancellationToken, AuthenticationMode.Internal);

    internal Task<T> PostInternalAsync<T>(string path, object body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Post, path, Json(body), retryable: false, cancellationToken, AuthenticationMode.Internal);

    internal Task<T> PutInternalAsync<T>(string path, object body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Put, path, Json(body), retryable: false, cancellationToken, AuthenticationMode.Internal);

    internal Task<T> PostAsync<T>(string path, HttpContent body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Post, path, body, retryable: false, cancellationToken);

    internal Task<T> PutAsync<T>(string path, object body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Put, path, Json(body), retryable: false, cancellationToken);

    internal Task DeleteAsync(string path, CancellationToken cancellationToken) =>
        SendAsync<JsonElement>(HttpMethod.Delete, path, null, retryable: true, cancellationToken);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body, PostizJson.Options), Encoding.UTF8, "application/json");

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        HttpContent? content,
        bool retryable,
        CancellationToken cancellationToken,
        AuthenticationMode authenticationMode = AuthenticationMode.ApiKey)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path) { Content = content };
            if (authenticationMode == AuthenticationMode.ApiKey && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", options.ApiKey);
                if (!string.IsNullOrWhiteSpace(options.OrganizationId))
                {
                    request.Headers.TryAddWithoutValidation("X-HappyM-Organization-Id", options.OrganizationId);
                }
            }
            else if (authenticationMode == AuthenticationMode.Internal)
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {options.InternalClientSecret}");
                request.Headers.TryAddWithoutValidation("X-HappyM-Client-Id", options.InternalClientId);
            }
            request.Headers.Accept.ParseAdd("application/json");

            var correlationId = options.CorrelationIdFactory?.Invoke();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
            }

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (response.Content.Headers.ContentLength == 0)
                {
                    return default!;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                return (await JsonSerializer.DeserializeAsync<T>(stream, PostizJson.Options, timeout.Token).ConfigureAwait(false))!;
            }

            if (retryable && attempt < options.MaxRetryAttempts && IsTransient(response.StatusCode))
            {
                await Task.Delay(GetRetryDelay(response, attempt), timeout.Token).ConfigureAwait(false);
                continue;
            }

            throw await CreateExceptionAsync(response, timeout.Token).ConfigureAwait(false);
        }
    }

    private enum AuthenticationMode
    {
        None,
        ApiKey,
        Internal,
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delta;
        }

        return TimeSpan.FromMilliseconds(Math.Min(250 * Math.Pow(2, attempt), 4_000));
    }

    private static async Task<PostizApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var redacted = PostizErrorSanitizer.Redact(raw);
        var code = PostizErrorSanitizer.ExtractCode(raw);
        var correlationId = response.Headers.TryGetValues("X-Correlation-Id", out var values)
            ? values.FirstOrDefault()
            : null;
        return new PostizApiException(response.StatusCode, code, correlationId, redacted);
    }
}
