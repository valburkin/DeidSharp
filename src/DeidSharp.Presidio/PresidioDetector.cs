using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DeidSharp.Detection;

namespace DeidSharp.Presidio;

// Design pattern: Adapter.
// The pattern: a wrapper that converts one interface into the one callers expect.
// Here: Presidio speaks HTTP + JSON with Python code-point offsets and its own entity
// names. This class turns that into IPhiDetector spans (UTF-16 offsets, DeidSharp
// categories), so a Presidio sidecar stacks with any other detector.

/// <summary>
/// Finds PHI with a Presidio analyzer service: the stock image (spaCy NER plus
/// Presidio's pattern recognizers) or the DeidSharp clinical sidecar (a transformer
/// model trained on clinical notes). Fails closed: an unreachable or erroring service
/// throws <see cref="PhiDetectionException"/>, never "no PHI found".
/// </summary>
public sealed partial class PresidioDetector : IPhiDetector
{
    private readonly HttpClient _http;
    private readonly PresidioOptions _options;

    public PresidioDetector(HttpClient http, PresidioOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _http = http;
        _options = options;
    }

    public string Name => _options.Name;

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Text.Length == 0)
        {
            return [];
        }

        var results = await AnalyzeAsync(request.Text, cancellationToken).ConfigureAwait(false);
        return ToSpans(request.Text, results);
    }

    /// <summary>
    /// True if the analyzer answers its health endpoint within <see cref="PresidioOptions.Timeout"/>.
    /// Any failure (unreachable, an error status, a timeout, an open circuit breaker in the
    /// HTTP pipeline) is false; only the caller's own cancellation throws.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _http.GetAsync(new Uri(_options.Endpoint, "health"), timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    // One call to /analyze. Every way it can fail becomes a PhiDetectionException.
    private async Task<List<AnalyzeResult>> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        var body = new AnalyzeRequest(
            text,
            _options.Language,
            _options.ScoreThreshold,
            _options.Entities is { Count: > 0 } entities ? [.. entities] : null,
            _options.ReportRecognizers ? true : null);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _http.PostAsJsonAsync(new Uri(_options.Endpoint, "analyze"), body, PresidioJson.Default.AnalyzeRequest, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // The body is not included: an error page may echo the submitted text.
                throw new PhiDetectionException($"Presidio analyzer '{Name}' returned HTTP {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync(PresidioJson.Default.ListAnalyzeResult, timeout.Token).ConfigureAwait(false)
                ?? throw new PhiDetectionException($"Presidio analyzer '{Name}' returned an empty response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PhiDetectionException($"Presidio analyzer '{Name}' did not answer within {_options.Timeout}.");
        }
        catch (HttpRequestException ex)
        {
            throw new PhiDetectionException($"Presidio analyzer '{Name}' is unreachable.", ex);
        }
        catch (JsonException)
        {
            // Not passed on: the parser's message can quote the response, which an analyzer may echo the text into.
            throw new PhiDetectionException($"Presidio analyzer '{Name}' returned a response that is not a result list.");
        }
        catch (Exception ex) when (ex is not (PhiDetectionException or OperationCanceledException))
        {
            // A handler in the HTTP pipeline can fail in its own way: a retry policy's timeout, an
            // open circuit breaker, a full rate limiter. Only its type is named: a handler of the
            // caller's could hold the request, and with it the text.
            throw new PhiDetectionException($"Presidio analyzer '{Name}' failed in the HTTP pipeline with {ex.GetType().Name}.");
        }
    }

    private List<PhiSpan> ToSpans(string text, List<AnalyzeResult> results)
    {
        var index = new CodePointIndex(text);
        var spans = new List<PhiSpan>(results.Count);

        // A result list can hold a JSON null, whatever the declared type says.
        foreach (var result in results)
        {
            if (result is null || string.IsNullOrWhiteSpace(result.EntityType))
            {
                throw new PhiDetectionException($"Presidio analyzer '{Name}' returned a result without an entity type.");
            }

            if (_options.IgnoredEntities.Contains(result.EntityType))
            {
                continue;
            }

            var start = index.ToUtf16(result.Start);
            var end = index.ToUtf16(result.End);
            if (start < 0 || end < 0 || end <= start)
            {
                throw new PhiDetectionException(
                    $"Presidio analyzer '{Name}' returned offsets [{result.Start}, {result.End}) that do not fit the text.");
            }

            var category = PresidioEntityMap.Resolve(result.EntityType, _options.EntityMap);
            (start, end) = SpanWidening.Widen(text, start, end, category);
            var score = double.IsNaN(result.Score) ? 0 : Math.Clamp(result.Score, 0, 1);
            spans.Add(new PhiSpan(start, end, category, score, Source(result)));
        }

        return spans;
    }

    // The detector's name, and with ReportRecognizers the recognizer's. A name that isn't a
    // plain identifier is left out: sources reach logs and HTTP responses, and a sidecar's
    // answer is never echoed there unchecked.
    private string Source(AnalyzeResult result)
    {
        if (!_options.ReportRecognizers)
        {
            return Name;
        }

        var recognizer = result.Metadata?.RecognizerName ?? result.Explanation?.Recognizer;
        return recognizer is not null && PlainName().IsMatch(recognizer) ? $"{Name}:{recognizer}" : Name;
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_.\-]{1,64}\z")]
    private static partial Regex PlainName();

    // Wire format of Presidio's /analyze request and of one result in its response.
    private sealed record AnalyzeRequest(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("score_threshold")] double ScoreThreshold,
        [property: JsonPropertyName("entities")] string[]? Entities,
        [property: JsonPropertyName("return_decision_process")] bool? ReturnDecisionProcess);

    // Which recognizer found a result: in recognition_metadata (the DeidSharp sidecar), or in
    // analysis_explanation when the decision process was asked for (stock Presidio).
    private sealed record AnalyzeResult(
        [property: JsonPropertyName("entity_type")] string EntityType,
        [property: JsonPropertyName("start")] int Start,
        [property: JsonPropertyName("end")] int End,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("recognition_metadata")] RecognitionMetadata? Metadata = null,
        [property: JsonPropertyName("analysis_explanation")] AnalysisExplanation? Explanation = null);

    private sealed record RecognitionMetadata([property: JsonPropertyName("recognizer_name")] string? RecognizerName);

    private sealed record AnalysisExplanation([property: JsonPropertyName("recognizer")] string? Recognizer);

    // Serialization code generated at compile time: no reflection, safe to trim.
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AnalyzeRequest))]
    [JsonSerializable(typeof(List<AnalyzeResult>))]
    private sealed partial class PresidioJson : JsonSerializerContext;
}
