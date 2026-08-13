using System.Text.Json;
using Postiz.Transport;

namespace Postiz.Chat;

public sealed record PostizChatMessageRequest(string Message, string? ThreadId = null);

public sealed record PostizChatReply(string ThreadId, string Message);

public sealed record PostizChatThread(string ThreadId, JsonElement[] Messages);

public interface IPostizChatClient
{
    Task<PostizChatReply> SendMessageAsync(
        PostizChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<PostizChatThread> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default);
}

internal sealed class PostizChatClient(PostizTransport transport) : IPostizChatClient
{
    public Task<PostizChatReply> SendMessageAsync(
        PostizChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);
        return transport.PostAsync<PostizChatReply>("public/v1/chat/messages", request, cancellationToken);
    }

    public Task<PostizChatThread> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        return transport.GetAsync<PostizChatThread>(
            $"public/v1/chat/threads/{Uri.EscapeDataString(threadId)}",
            cancellationToken);
    }
}
