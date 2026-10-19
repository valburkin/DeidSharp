using System.Net;
using System.Text;

namespace DeidSharp.Tests.TestDoubles;

// Design pattern: Test Double (Fake, Spy).
// The pattern: a stand-in for a real dependency in a test. A fake works but takes a
// shortcut; a spy records how it was called.
// Here: an HTTP handler that answers with a canned status and body instead of a real
// Presidio service, and records each request so tests can check what was sent.

/// <summary>Answers every request with a fixed response (or throws), and records requests.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public FakeHttpHandler(HttpStatusCode status, string body)
        : this(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") })
    {
    }

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri, body));
        return respond(request);
    }
}
