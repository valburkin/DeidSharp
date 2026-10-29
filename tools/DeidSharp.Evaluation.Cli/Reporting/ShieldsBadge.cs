using System.Globalization;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// docs/evaluation/badge.json, in shields.io's endpoint format: the README's badge shows the
/// recall of one named stack, the deterministic detectors with the clinical sidecar (the stack
/// docs/providers.md recommends), on the held-out half of the nursing notes (PhysioNet's
/// Deidentified Medical Text) in the Safe Harbor scope. The stack is fixed and named on the badge, so the number never silently
/// comes from another one.
/// </summary>
internal sealed record ShieldsBadge(int SchemaVersion, string Label, string Message, string Color)
{
    private const string HeadlineCorpus = "physionet-deid";
    private const string HeadlineScope = "safe-harbor";
    private const string HeadlineSplit = "report";
    private const string HeadlineStack = "deterministic + clinical";
    private const string BadgeLabel = "PHI recall (nursing notes)";

    public static ShieldsBadge From(PublishedResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var headline = results.Corpora.FirstOrDefault(c => c is { Corpus: HeadlineCorpus, Scope: HeadlineScope, Split: HeadlineSplit });
        return headline?.Stacks.FirstOrDefault(s => s.Stack == HeadlineStack) is { } stack
            ? new ShieldsBadge(1, BadgeLabel, $"{Truncated(stack.Recall)}% · {HeadlineStack} · v{headline.Version}", "blue")
            : new ShieldsBadge(1, BadgeLabel, "not measured yet", "lightgrey");
    }

    // Rounded down to one decimal: 99.96% shows as 99.9%, never as 100.0%.
    private static string Truncated(double rate) => (Math.Floor((decimal)rate * 1000m) / 10m).ToString("0.0", CultureInfo.InvariantCulture);
}
