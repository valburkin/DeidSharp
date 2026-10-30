using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// The stacks and corpora the README's badge and its results at a glance show: the recommended
/// configurations of record (findings F37 and D6), named exactly as the tool names them, so a
/// number never silently comes from another stack. A stack or corpus missing from results.json
/// shows as not measured.
/// </summary>
internal static class HeadlineStacks
{
    /// <summary>The corpus the badge shows: the nursing notes, whose held-out half is the oldest measurement of record.</summary>
    public const string BadgeCorpus = "physionet-deid";

    /// <summary>The recommended stack without a language model: the deterministic detectors with the gazetteer, and the clinical sidecar.</summary>
    public const string WithoutLlm = "deterministic + clinical";

    /// <summary>The deterministic detectors without the gazetteer (D3) and the language model with all forty examples.</summary>
    private const string PrecisionFirst = "deterministic without gazetteer + llm gemma4:26b-a4b (all-examples)";

    /// <summary>The precision-first stack with the clinical sidecar.</summary>
    public const string RecallFirst = "deterministic without gazetteer + clinical + llm gemma4:26b-a4b (all-examples)";

    /// <summary>The rows of the README's table: a reader's name for each stack, and the tool's.</summary>
    public static IReadOnlyList<(string Label, string Stack)> Stacks { get; } =
    [
        ("Recall first: deterministic + clinical + LLM", RecallFirst),
        ("Precision first: deterministic + LLM", PrecisionFirst),
        ("Without an LLM: deterministic + clinical", WithoutLlm),
    ];

    /// <summary>
    /// The headline stacks a run that publishes would not measure: the stack without a model
    /// always, the recall-first and precision-first ones when the run has a model, and any
    /// <paramref name="published"/> already holds. Publishing replaces a corpus's results, so a
    /// run without them would take them off the README.
    /// </summary>
    public static IReadOnlyList<string> MissingFrom(IEnumerable<string> measured, bool withLlm, CorpusResult? published = null)
    {
        var names = measured.ToHashSet(StringComparer.Ordinal);
        var publishedNames = published?.Stacks.Select(s => s.Stack).ToHashSet(StringComparer.Ordinal) ?? [];
        return
        [
            .. Stacks.Select(s => s.Stack)
                .Where(stack => stack == WithoutLlm || withLlm || publishedNames.Contains(stack))
                .Where(stack => !names.Contains(stack)),
        ];
    }

    /// <summary>The columns: the held-out halves of the two real corpora, and all of ASQ-PHI, which has none.</summary>
    public static IReadOnlyList<(string Label, string Corpus, string Split)> Corpora { get; } =
    [
        ("Nursing notes", BadgeCorpus, CorpusSplits.Id(CorpusSplit.Report)),
        ("Discharge summaries", "mimic-iv-note", CorpusSplits.Id(CorpusSplit.Report)),
        ("ASQ-PHI", "asq-phi", CorpusSplits.Id(CorpusSplit.All)),
    ];

    /// <summary>The measurement of one column in the Safe Harbor scope, if published.</summary>
    public static CorpusResult? Find(PublishedResults results, string corpus, string split)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Find(corpus, EvaluationScopes.Id(EvaluationScope.SafeHarbor), split);
    }
}
