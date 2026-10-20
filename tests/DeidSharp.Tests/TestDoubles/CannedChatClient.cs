using Microsoft.Extensions.AI;

namespace DeidSharp.Tests.TestDoubles;

// Design pattern: Test Double (Stub).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned
// answers; a spy records how it was called.
// Here: a chat client that answers every request with the same JSON, so examples that
// need a language model run without one.

/// <summary>Answers every chat request with one fixed reply.</summary>
internal sealed class CannedChatClient(string reply) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)) { FinishReason = ChatFinishReason.Stop });

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
