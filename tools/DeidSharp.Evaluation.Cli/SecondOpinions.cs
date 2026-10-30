using DeidSharp.Llm;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each kind of second opinion an LLM stack can be measured with is one row (what the
// model may veto, the label of its stack, and whether it is an experiment, which --publish
// refuses), so a new kind is a row, never a branch in DetectorStacks.

/// <summary>The second opinions <c>--llm-second-opinion</c> can name, so their costs can be compared in one run.</summary>
internal static class SecondOpinions
{
    private static readonly Dictionary<string, (string Label, Func<PhiSpan, bool> MayVeto, bool Experiment)> Rows = new(StringComparer.Ordinal)
    {
        // The library's default: never a known identifier's, a pattern's or the LLM's own span,
        // never a location or organization (F26).
        ["on"] = ("second opinion", LlmSecondOpinionOptions.Default.MayVeto, false),

        // A self-check: the same bars, but the LLM's own spans may be vetoed too. An experiment (F31).
        ["self"] = ("second opinion with self-check", span =>
            span.Category is not (PhiCategory.Location or PhiCategory.Organization)
            && !span.Sources.Any(s => s == "known" || s.StartsWith("pattern", StringComparison.Ordinal)), true),
    };

    public static IEnumerable<string> Ids => Rows.Keys;

    public static bool Knows(string id) => Rows.ContainsKey(id);

    /// <summary>Whether the second opinion is an experiment, so its results can't be published.</summary>
    public static bool IsExperiment(string id) => Rows[id].Experiment;

    public static (string Label, Func<PhiSpan, bool> MayVeto) Get(string id)
    {
        var (label, mayVeto, _) = Rows[id];
        return (label, mayVeto);
    }
}
