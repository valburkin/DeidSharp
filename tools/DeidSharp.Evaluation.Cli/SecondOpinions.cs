using DeidSharp.Llm;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each kind of second opinion an LLM stack can be measured with is one row (what the
// model may veto, and the label of its stack), so a new kind is a row, never a branch in
// DetectorStacks.

/// <summary>The second opinions <c>--llm-second-opinion</c> can name, so their costs can be compared in one run.</summary>
internal static class SecondOpinions
{
    private static readonly Dictionary<string, (string Label, Func<PhiSpan, bool> MayVeto)> Rows = new(StringComparer.Ordinal)
    {
        // The library's default: never a known identifier's, a pattern's or the LLM's own span,
        // never a location or organization (F26).
        ["on"] = ("second opinion", LlmSecondOpinionOptions.Default.MayVeto),

        // A self-check: the same bars, but the LLM's own spans may be vetoed too.
        ["self"] = ("second opinion with self-check", span =>
            span.Category is not (PhiCategory.Location or PhiCategory.Organization)
            && !span.Sources.Any(s => s == "known" || s.StartsWith("pattern", StringComparison.Ordinal))),
    };

    public static IEnumerable<string> Ids => Rows.Keys;

    public static bool Knows(string id) => Rows.ContainsKey(id);

    public static (string Label, Func<PhiSpan, bool> MayVeto) Get(string id) => Rows[id];
}
