namespace DeidSharp.Evaluation.Cli.Corpora;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each corpus label is one LabelRule row: the DeidSharp category it means and
// whether Safe Harbor requires removing it. A corpus with other labels brings other
// rows, never a branch in the scorer.

/// <summary>What a corpus's own labels mean in DeidSharp's categories.</summary>
internal sealed class LabelMap
{
    private readonly Dictionary<string, LabelRule> _rules;

    public LabelMap(IEnumerable<LabelRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToDictionary(r => r.Label, StringComparer.Ordinal);
    }

    public bool Knows(string label) => _rules.ContainsKey(label);

    /// <summary>True if a span with this label counts under <paramref name="scope"/>.</summary>
    public bool Counts(string label, EvaluationScope scope) => scope == EvaluationScope.All || Rule(label).SafeHarbor;

    /// <summary>The document with the gold spans that count under <paramref name="scope"/>, in DeidSharp's categories.</summary>
    public AnnotatedText ToAnnotated(CorpusDocument document, EvaluationScope scope)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new AnnotatedText(
            document.Text,
            [.. document.Spans.Where(s => Counts(s.Label, scope)).Select(s => new GoldSpan(s.Start, s.End, Rule(s.Label).Category))]);
    }

    // Readers check labels with line numbers as they read; this only guards a reader that didn't.
    private LabelRule Rule(string label) => _rules.TryGetValue(label, out var rule)
        ? rule
        : throw new CorpusFormatException("A gold span carries a label the corpus's label map doesn't list.");
}

/// <summary>One corpus label: the DeidSharp category it means, and whether DeidSharp's Safe Harbor scope counts it.</summary>
internal sealed record LabelRule(string Label, PhiCategory Category, bool SafeHarbor);
