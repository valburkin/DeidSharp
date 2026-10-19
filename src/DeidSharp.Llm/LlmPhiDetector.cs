using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DeidSharp.Detection;
using Microsoft.Extensions.AI;

namespace DeidSharp.Llm;

// Design pattern: Adapter.
// The pattern: a class that gives an existing object the interface its callers expect,
// translating each call, so two pieces that weren't made for each other work together.
// Here: a chat model (Microsoft.Extensions.AI's IChatClient) becomes an IPhiDetector. The
// detector turns a text into chat messages, and the model's JSON answer back into spans,
// so a language model stacks in a CompositePhiDetector like any other detector.

/// <summary>
/// Finds PHI by asking a language model (<see cref="IChatClient"/>) to list it, then locating
/// each piece it lists in the text. Meant for a model running where the text may go, such as a
/// local Ollama, vLLM or LM Studio: every text is sent to the client as it is.
/// </summary>
/// <remarks>
/// <para>
/// The model answers in JSON with the pieces copied from the text. A piece is found wherever it
/// occurs as whole words; a piece that isn't in the text (the model made it up or reworded it)
/// is dropped. Long texts are sent in overlapping pieces (<see cref="LlmDetectorOptions.ChunkLength"/>).
/// </para>
/// <para>
/// Fails closed: an error from the client, or an answer that isn't the JSON asked for, throws
/// <see cref="PhiDetectionException"/>, whose message never quotes the answer. It asks at
/// temperature 0, but a model can still answer differently on another run or version, so it
/// never belongs in the leak guard.
/// </para>
/// </remarks>
public sealed partial class LlmPhiDetector : IPhiDetector
{
    // The model may only answer with this.
    private static readonly ChatResponseFormat AnswerFormat = ChatResponseFormat.ForJsonSchema(
        JsonDocument.Parse(
            """
            {"type": "object", "required": ["phi"], "properties": {"phi": {"type": "array", "items": {
              "type": "object", "required": ["text", "type"], "properties": {
                "text": {"type": "string"},
                "type": {"type": "string", "enum": ["name", "date", "age", "location", "phone", "email", "url", "id", "other"]}}}}}}
            """).RootElement,
        "phi");

    private static readonly HashSet<string> Months = new(StringComparer.Ordinal) { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

    private static readonly Dictionary<string, PhiCategory> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = PhiCategory.Name,
        ["date"] = PhiCategory.Date,
        ["age"] = PhiCategory.Age,
        ["location"] = PhiCategory.Location,
        ["phone"] = PhiCategory.Phone,
        ["email"] = PhiCategory.Email,
        ["url"] = PhiCategory.Url,
    };

    // The smallest piece asked again in halves when its answer is cut off.
    private const int MinimumChunkLength = 200;

    private readonly IChatClient _client;
    private readonly LlmDetectorOptions _options;
    private readonly List<ChatMessage> _preamble;

    public LlmPhiDetector(IChatClient client, LlmDetectorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = options ?? LlmDetectorOptions.Default;
        _options.Validate();

        // The instructions, then each example as a question and its answer.
        _preamble = [new ChatMessage(ChatRole.System, _options.Prompt.Instructions), .. Shown(_options.Prompt.Examples)];
    }

    // Each example as the model's question and the answer it should give.
    private static IEnumerable<ChatMessage> Shown(IEnumerable<LlmExample> examples) => examples.SelectMany(example => new[]
    {
        new ChatMessage(ChatRole.User, example.Text),
        new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(
            new LlmAnswer([.. example.Phi.Select(f => new LlmAnswerItem(f.Text, f.Type))]), LlmJson.Default.LlmAnswer)),
    });

    public string Name => _options.Name;

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spans = new List<PhiSpan>();
        var pieces = new Stack<(int Start, int Length)>(Chunks(request.Text, _options.ChunkLength, _options.ChunkOverlap).Reverse());
        while (pieces.TryPop(out var piece))
        {
            var chunk = request.Text.Substring(piece.Start, piece.Length);
            var options = new ChatOptions { Temperature = 0, MaxOutputTokens = _options.MaxOutputTokens, ResponseFormat = AnswerFormat };
            _options.ConfigureRequest?.Invoke(options);
            var retrieved = _options.Retriever is { } retriever ? Shown(retriever.Retrieve(chunk, _options.RetrievedExamples)) : [];
            var response = await _client.GetResponseAsync([.. _preamble, .. retrieved, new ChatMessage(ChatRole.User, chunk)], options, cancellationToken)
                .ConfigureAwait(false);

            // An answer cut off at the output limit (a long list, or a model repeating itself) is
            // asked again in two halves, down to MinimumChunkLength; below that it fails closed.
            var cutOff = response.FinishReason == ChatFinishReason.Length;
            if (cutOff && piece.Length >= 2 * MinimumChunkLength)
            {
                // The halves overlap by at most a quarter of the piece, so each is shorter than it.
                var overlap = Math.Min(_options.ChunkOverlap, piece.Length / 4);
                foreach (var half in Chunks(chunk, piece.Length / 2 + overlap, overlap).Reverse())
                {
                    pieces.Push((piece.Start + half.Start, half.Length));
                }

                continue;
            }

            foreach (var finding in Parse(response.Text, cutOff))
            {
                var category = finding.Type is { } type ? Categories.GetValueOrDefault(type, PhiCategory.OtherIdentifier) : PhiCategory.OtherIdentifier;
                if (_options.SafeHarborOnly && BeyondSafeHarbor(category, finding.Text))
                {
                    continue;
                }

                spans.AddRange(Locate(chunk, finding.Text).Select(at => new PhiSpan(piece.Start + at.Start, piece.Start + at.End, category, 0.7, Name)));
            }
        }

        return spans;
    }

    // Pieces of at most `length` characters, ending at a space where one is near, overlapping by `overlap`.
    private static IEnumerable<(int Start, int Length)> Chunks(string text, int length, int overlap)
    {
        var start = 0;
        while (true)
        {
            var end = Math.Min(start + length, text.Length);
            if (end < text.Length)
            {
                var space = text.LastIndexOfAny([' ', '\n', '\t'], end - 1, end - start - length / 2);
                end = space > start ? space + 1 : end;
            }

            yield return (start, end - start);
            if (end >= text.Length)
            {
                yield break;
            }

            start = Math.Max(end - overlap, start + 1);
        }
    }

    // Every whole-word occurrence of the piece, as written, or else ignoring case. None if the model made it up.
    private static List<(int Start, int End)> Locate(string text, string? piece)
    {
        piece = piece?.Trim();
        if (string.IsNullOrEmpty(piece) || piece.Length < 2)
        {
            return [];
        }

        foreach (var comparison in (StringComparison[])[StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase])
        {
            var found = new List<(int Start, int End)>();
            for (var at = text.IndexOf(piece, comparison); at >= 0; at = text.IndexOf(piece, at + 1, comparison))
            {
                var end = at + piece.Length;
                if (WholeWords(text, at, end))
                {
                    found.Add((at, end));
                }
            }

            if (found.Count > 0)
            {
                return found;
            }
        }

        return [];
    }

    // What Safe Harbor keeps, whichever type the model gave it: ages under 90; as a date, a year or
    // a time on its own, or words with no number or month ("today", "Monday"); a short number as an id.
    private static bool BeyondSafeHarbor(PhiCategory category, string? text)
    {
        var value = (text?.Trim() ?? "").ToLowerInvariant();
        var digits = value.All(char.IsAsciiDigit);
        return category switch
        {
            PhiCategory.Age => SafeHarborAges.IsUnder90(value),
            PhiCategory.Date => (digits && value.Length == 4) || TimeOfDay().IsMatch(value) || !HasDayOrMonth(value),
            PhiCategory.OtherIdentifier => digits && value.Length is > 0 and <= 3,
            _ => false,
        };
    }

    // A date needs a number or a month's name ("3/14", "the 2nd", "March"); "today" and "Monday" are not PHI. Lowercase input.
    private static bool HasDayOrMonth(string value) =>
        value.Any(char.IsAsciiDigit)
        || value.Split([' ', '.', ',', '-', '/'], StringSplitOptions.RemoveEmptyEntries).Any(w => w.Length >= 3 && Months.Contains(w[..3]));

    private static bool WholeWords(string text, int start, int end) =>
        (start == 0 || !char.IsLetterOrDigit(text[start - 1]) || !char.IsLetterOrDigit(text[start]))
        && (end == text.Length || !char.IsLetterOrDigit(text[end]) || !char.IsLetterOrDigit(text[end - 1]));

    private IReadOnlyList<LlmAnswerItem> Parse(string answer, bool cutOff)
    {
        // Safe to say, and tells a person what to tune (MaxOutputTokens, ChunkLength).
        var why = cutOff ? " It was cut off at the output limit." : "";
        // Some models wrap JSON in a code fence or a sentence despite the format asked for.
        var first = answer.IndexOf('{', StringComparison.Ordinal);
        var last = answer.LastIndexOf('}');
        try
        {
            var parsed = first >= 0 && last > first ? JsonSerializer.Deserialize(answer.AsSpan(first, last - first + 1), LlmJson.Default.LlmAnswer) : null;
            return parsed?.Phi ?? throw new PhiDetectionException($"Detector '{Name}' got an answer without the list of PHI asked for.{why}");
        }
        catch (JsonException)
        {
            // The parser's message can quote the answer, which holds PHI: say only that it failed.
            throw new PhiDetectionException($"Detector '{Name}' got an answer that isn't the JSON asked for.{why}");
        }
    }

    /// <summary>The model's answer: the PHI it found. Any field may be missing from what a model writes.</summary>
    internal sealed record LlmAnswer(IReadOnlyList<LlmAnswerItem>? Phi);

    /// <summary>One piece of PHI in the model's answer.</summary>
    internal sealed record LlmAnswerItem(string? Text, string? Type);

    // A time of day on its own: "8pm", "08:30", "2 am", "1400 hrs".
    [GeneratedRegex(@"\A(?:\d{1,2}(?::\d{2})?\s*(?:am|pm|a\.m\.|p\.m\.)|\d{1,2}:\d{2}(?::\d{2})?|\d{4}\s*(?:h|hr|hrs|hours))\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimeOfDay();


    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(LlmAnswer))]
    internal sealed partial class LlmJson : JsonSerializerContext;
}
