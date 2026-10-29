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
// sends to Ollama (its context window) without a model.

/// <summary>Answers <c>/api/chat</c> as Ollama does, with the answer a test picks for each request.</summary>
internal sealed class FakeOllamaServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<JsonElement, string> _answer;
    private readonly ConcurrentQueue<JsonElement> _requests = new();
    private readonly ConcurrentQueue<Exception> _errors = new();
    private readonly Task _serving;

    /// <param name="answer">The model's answer to a request, given the request's JSON body.</param>
    public FakeOllamaServer(Func<JsonElement, string> answer)
    {
        _answer = answer;
        (_listener, Endpoint) = Listen();
        _serving = Task.Run(ServeAsync);
    }

    public Uri Endpoint { get; }

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
        var bytes = Encoding.UTF8.GetBytes(reply + "\n");
        context.Response.ContentType = "application/x-ndjson";
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}
