using System.Text.RegularExpressions;
using DeidSharp.Llm;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one IExampleRetriever, by word similarity: no model, the same answer every time.

/// <summary>
/// Picks the examples whose words are most like the text's, by TF-IDF cosine similarity over
/// lowercase words and numbers. Deterministic and needs no embedding model; for retrieving
/// few-shot examples for entity extraction it does as well as dense embeddings in published
/// comparisons. Ties keep the pool's order.
/// </summary>
/// <remarks>
/// An experiment, kept here so <c>--llm-prompts retrieved</c> reproduces F27: four retrieved
/// examples added precision but not recall, and every example of the pool in every prompt
/// (<see cref="LlmPrompt.WithPoolExamples"/>) did better, so the libraries don't ship it.
/// </remarks>
internal sealed partial class TfIdfExampleRetriever : IExampleRetriever
{
    private readonly IReadOnlyList<LlmExample> _pool;
    private readonly Dictionary<string, double> _idf;
    private readonly List<Dictionary<string, double>> _vectors;

    public TfIdfExampleRetriever(IReadOnlyList<LlmExample> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        var documents = pool.Select(e => Words(e.Text)).ToList();
        _idf = documents.SelectMany(d => d.Distinct(StringComparer.Ordinal)).GroupBy(w => w, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Math.Log((1.0 + documents.Count) / (1.0 + g.Count())) + 1.0, StringComparer.Ordinal);
        _vectors = [.. documents.Select(Vector)];
    }

    public IReadOnlyList<LlmExample> Retrieve(string text, int count)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var query = Vector(Words(text));
        return [.. _vectors.Select((v, i) => (Index: i, Score: Cosine(query, v)))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Index)
            .Take(count)
            .Select(x => _pool[x.Index])];
    }

    private Dictionary<string, double> Vector(List<string> words)
    {
        // Words the pool never uses carry no weight: they can't make two texts alike.
        var vector = words.Where(_idf.ContainsKey).GroupBy(w => w, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count() * _idf[g.Key], StringComparer.Ordinal);
        var norm = Math.Sqrt(vector.Values.Sum(v => v * v));
        return norm == 0 ? vector : vector.ToDictionary(kv => kv.Key, kv => kv.Value / norm, StringComparer.Ordinal);
    }

    private static double Cosine(Dictionary<string, double> a, Dictionary<string, double> b) =>
        a.Count <= b.Count ? a.Sum(kv => b.TryGetValue(kv.Key, out var v) ? kv.Value * v : 0) : Cosine(b, a);

    private static List<string> Words(string text) => [.. Word().Matches(text.ToLowerInvariant()).Select(m => m.Value)];

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}
