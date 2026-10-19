using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeidSharp.Detection;
using Microsoft.Extensions.AI;

namespace DeidSharp.Llm;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IPhiDetector around a whole detector stack. After the stack has run, it shows
// a language model the spans that only the noisier detectors found, in their context, and
// drops the ones the model says are not PHI.

/// <summary>
/// A second opinion on a stack's findings: the model sees the note and each candidate span
/// that <see cref="LlmSecondOpinionOptions.MayVeto"/> allows it to judge (by default, spans no
/// known-identifier, pattern or LLM detector reported), and lists the ones that are not PHI.
/// Only those are dropped: a span the model doesn't mention stays.
/// </summary>
/// <remarks>
/// It trades recall for precision: a span the model wrongly calls "not PHI" is lost. DeidSharp
/// replaces every span any detector reports (recall first), so use it only where its measured
/// cost in recall is acceptable. Fails closed like <see cref="LlmPhiDetector"/>: an answer
/// that isn't the JSON asked for throws <see cref="PhiDetectionException"/> without quoting it.
/// </remarks>
public sealed partial class LlmSecondOpinion : IPhiDetector
{
    private const string Instructions =
        """
        You check candidates for protected health information (PHI) in a clinical note. A detector marked each candidate, and some are mistakes.
        PHI here means: a name of any person (patient, relative, friend or staff), a date other than a year on its own, an age over 89, a named place smaller than a state (hospital, clinic, ward named after a person or place, city, town, street, ZIP code), a phone or fax number, an email or web address, or an identifying number (record, account, health plan, license, device).
        For each numbered candidate, decide from the note whether the marked text, as it is used there, is PHI. Medical terms, drugs, devices, eponyms, abbreviations, units, times, ordinary words and numbers that are measurements are not PHI.
        When you are unsure, treat it as PHI.
        Answer only with JSON: {"not_phi": [numbers of the candidates that are not PHI]}. If all are PHI, answer {"not_phi": []}.
        """;

    private static readonly ChatResponseFormat AnswerFormat = ChatResponseFormat.ForJsonSchema(
        JsonDocument.Parse(
            """{"type": "object", "required": ["not_phi"], "properties": {"not_phi": {"type": "array", "items": {"type": "integer"}}}}""").RootElement,
        "verdicts");

    private readonly IPhiDetector _inner;
    private readonly IChatClient _client;
    private readonly LlmSecondOpinionOptions _options;

    public LlmSecondOpinion(IPhiDetector inner, IChatClient client, LlmSecondOpinionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(client);
        _inner = inner;
        _client = client;
        _options = options ?? LlmSecondOpinionOptions.Default;
        _options.Validate();
    }

    public string Name => _inner.Name;

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spans = await _inner.DetectAsync(request, cancellationToken).ConfigureAwait(false);
        var candidates = spans.Where(_options.MayVeto).ToList();
        if (candidates.Count == 0)
        {
            return spans;
        }

        var vetoed = new HashSet<PhiSpan>();
        foreach (var batch in candidates.Chunk(_options.MaxCandidatesPerCall))
        {
            var question = new StringBuilder("Note:\n").Append(request.Text).Append("\n\nCandidates:\n");
            for (var i = 0; i < batch.Length; i++)
            {
                question.Append(CultureInfo.InvariantCulture, $"{i + 1}. \"{request.Text[batch[i].Start..batch[i].End]}\" ({batch[i].Category}) in: ...{InContext(request.Text, batch[i])}...\n");
            }

            var options = new ChatOptions { Temperature = 0, MaxOutputTokens = 512, ResponseFormat = AnswerFormat };
            _options.ConfigureRequest?.Invoke(options);
            var response = await _client.GetResponseAsync(
                [new ChatMessage(ChatRole.System, Instructions), new ChatMessage(ChatRole.User, question.ToString())], options, cancellationToken).ConfigureAwait(false);
            foreach (var number in NotPhi(response.Text).Where(n => n >= 1 && n <= batch.Length))
            {
                vetoed.Add(batch[number - 1]);
            }
        }

        return [.. spans.Where(s => !vetoed.Contains(s))];
    }

    // The span with up to 40 characters on each side, the span marked as [[...]], on one line.
    private static string InContext(string text, PhiSpan span)
    {
        var before = text[Math.Max(0, span.Start - 40)..span.Start];
        var after = text[span.End..Math.Min(text.Length, span.End + 40)];
        return $"{before}[[{text[span.Start..span.End]}]]{after}".ReplaceLineEndings(" ");
    }

    private IReadOnlyList<int> NotPhi(string answer)
    {
        var first = answer.IndexOf('{', StringComparison.Ordinal);
        var last = answer.LastIndexOf('}');
        try
        {
            var parsed = first >= 0 && last > first ? JsonSerializer.Deserialize(answer.AsSpan(first, last - first + 1), VerdictJson.Default.Verdict) : null;
            return parsed?.NotPhi ?? throw new PhiDetectionException($"Second opinion on '{Name}' got an answer without the verdicts asked for.");
        }
        catch (JsonException)
        {
            // The parser's message can quote the answer: say only that it failed.
            throw new PhiDetectionException($"Second opinion on '{Name}' got an answer that isn't the JSON asked for.");
        }
    }

    /// <summary>The model's answer: the candidates that are not PHI.</summary>
    internal sealed record Verdict([property: JsonPropertyName("not_phi")] IReadOnlyList<int>? NotPhi);

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(Verdict))]
    internal sealed partial class VerdictJson : JsonSerializerContext;
}
