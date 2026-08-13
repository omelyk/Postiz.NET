using System.Text.Json;
using Postiz.Transport;

namespace Postiz.Media;

public sealed record PostizMedia(string Id, string? Name, string Path, DateTimeOffset? CreatedAt = null);

public sealed record PostizMediaPage(int Pages, IReadOnlyList<PostizMedia> Results);

public sealed record PostizVideoFunctionRequest(string Identifier, string FunctionName, JsonElement Params);

public interface IPostizMediaClient
{
    Task<PostizMediaPage> ListAsync(
        int page = 1,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<PostizMedia> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<PostizMedia> UploadFromUrlAsync(Uri url, CancellationToken cancellationToken = default);

    Task<JsonElement> GenerateVideoAsync(JsonElement request, CancellationToken cancellationToken = default);

    Task<JsonElement> InvokeVideoFunctionAsync(
        PostizVideoFunctionRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class PostizMediaClient(PostizTransport transport) : IPostizMediaClient
{
    public Task<PostizMediaPage> ListAsync(
        int page = 1,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var query = $"page={page}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search.Trim())}";
        }

        return transport.GetAsync<PostizMediaPage>($"public/v1/media?{query}", cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return transport.DeleteAsync($"public/v1/media/{Uri.EscapeDataString(id)}", cancellationToken);
    }

    public async Task<PostizMedia> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(new NonDisposingStream(content));
        streamContent.Headers.ContentType = new(contentType);
        form.Add(streamContent, "file", fileName);
        return await transport.PostAsync<PostizMedia>("public/v1/upload", form, cancellationToken).ConfigureAwait(false);
    }

    public Task<PostizMedia> UploadFromUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return transport.PostAsync<PostizMedia>("public/v1/upload-from-url", new { url = url.ToString() }, cancellationToken);
    }

    public Task<JsonElement> GenerateVideoAsync(JsonElement request, CancellationToken cancellationToken = default) =>
        transport.PostAsync<JsonElement>("public/v1/generate-video", request, cancellationToken);

    public Task<JsonElement> InvokeVideoFunctionAsync(
        PostizVideoFunctionRequest request,
        CancellationToken cancellationToken = default) =>
        transport.PostAsync<JsonElement>("public/v1/video/function", request, cancellationToken);

    private sealed class NonDisposingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            inner.CopyToAsync(destination, bufferSize, cancellationToken);
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
