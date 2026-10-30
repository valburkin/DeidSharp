using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DeidSharp.Evaluation.Cli.Tests.TestDoubles;

// Design pattern: Test Double (Fake, Spy).
// The pattern: a stand-in for a real dependency in a test. A fake works but takes a
// shortcut; a spy records how it was called.
// Here: an HTTP server on this machine that answers Ollama's chat endpoint with canned
// JSON and keeps each request's body, so a test can check what the evaluation tool
// sends to Ollama (its context window) without a model; it also lists its models and
// its version, as the tool reads them to pin what it published.

/// <summary>Answers <c>/api/chat</c> as Ollama does, with the answer a test picks for each request, and <c>/api/tags</c> and <c>/api/version</c>.</summary>
internal sealed class FakeOllamaServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<JsonElement, string> _answer;
    private readonly ConcurrentQueue<JsonElement> _requests = new();
    private readonly ConcurrentQueue<Exception> _errors = new();
    private readonly Task _serving;

    private readonly IReadOnlyList<(string Name, string Digest)> _models;

    /// <param name="answer">The model's answer to a request, given the request's JSON body.</param>
    /// <param name="models">The models <c>/api/tags</c> lists, with their digests.</param>
    public FakeOllamaServer(Func<JsonElement, string> answer, IReadOnlyList<(string Name, string Digest)>? models = null)
    {
        _answer = answer;
        _models = models ?? [];
        (_listener, Endpoint) = Listen();
        _serving = Task.Run(ServeAsync);
    }

    public Uri Endpoint { get; }

    /// <summary>The version <c>/api/version</c> reports; a test can change it, as an update would.</summary>
    public string Version { get; set; } = "0.40.11";

    /// <summary>The JSON bodies of the chat requests, in the order they came.</summary>
    public IReadOnlyList<JsonElement> Requests => [.. _requests];

    /// <summary>What went wrong while answering a request, which the client saw only as a server error.</summary>
    public IReadOnlyList<Exception> Errors => [.. _errors];

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();

        // The serving loop ends once the listener stops; a fault it had surfaces here.
        _serving.Wait(TimeSpan.FromSeconds(5));
    }

    // Another process can take a free port between the check and the listener's start, so try a few.
    private static (HttpListener Listener, Uri Endpoint) Listen()
    {
        for (var attempt = 1; ; attempt++)
        {
            var endpoint = new Uri($"http://127.0.0.1:{FreePort()}/");
            var listener = new HttpListener();
            listener.Prefixes.Add(endpoint.ToString());
            try
            {
                listener.Start();
                return (listener, endpoint);
            }
            catch (HttpListenerException)
            {
                listener.Close();
                if (attempt == 5)
                {
                    throw;
                }
            }
        }
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            // A request it can't answer gets a server error at once, or a dropped connection if
            // the answer had begun, never a client left waiting; the server goes on to the next.
            try
            {
                await AnswerAsync(context);
            }
            catch (Exception e)
            {
                _errors.Enqueue(e);
                try
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch (Exception closing) when (closing is InvalidOperationException or HttpListenerException or ObjectDisposedException)
                {
                    context.Response.Abort();
                }
            }
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        if (context.Request.Url?.AbsolutePath is "/api/version" or "/api/tags")
        {
            var listing = context.Request.Url.AbsolutePath == "/api/version"
                ? JsonSerializer.Serialize(new { version = Version })
                : JsonSerializer.Serialize(new { models = _models.Select(m => new { name = m.Name, model = m.Name, digest = m.Digest, size = 1 }) });
            await ReplyAsync(context, "application/json", listing);
            return;
        }

        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = JsonDocument.Parse(await reader.ReadToEndAsync()).RootElement.Clone();
        _requests.Enqueue(body);

        var reply = JsonSerializer.Serialize(new
        {
            model = body.GetProperty("model").GetString(),
            created_at = "2026-01-01T00:00:00Z",
            message = new { role = "assistant", content = _answer(body) },
            done = true,
            done_reason = "stop",
        });
        await ReplyAsync(context, "application/x-ndjson", reply + "\n");
    }

    private static async Task ReplyAsync(HttpListenerContext context, string contentType, string body)
    {
        context.Response.ContentType = contentType;
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));
        context.Response.Close();
    }
}
