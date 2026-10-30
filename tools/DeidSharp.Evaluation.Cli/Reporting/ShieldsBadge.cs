using System.Globalization;
using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// docs/evaluation/badge.json, in shields.io's endpoint format: the README's badge shows the
/// recall of the recommended stacks (<see cref="HeadlineStacks"/>) on the held-out half of the
/// nursing notes (PhysioNet's Deidentified Medical Text) in the Safe Harbor scope: the
/// recall-first stack with a local language model and the stack without one, or the latter
/// alone when no stack with a model was published. The stacks are fixed, so the numbers never
/// silently come from another one; the README's table beside it gives their precision.
/// </summary>
internal sealed record ShieldsBadge(int SchemaVersion, string Label, string Message, string Color)
{
    private const string BadgeLabel = "PHI recall (nursing notes)";

    public static ShieldsBadge From(PublishedResults results)
    {
        var headline = HeadlineStacks.Find(results, HeadlineStacks.BadgeCorpus, CorpusSplits.Id(CorpusSplit.Report));
        var withoutLlm = headline?.Stacks.FirstOrDefault(s => s.Stack == HeadlineStacks.WithoutLlm);
        var recallFirst = headline?.Stacks.FirstOrDefault(s => s.Stack == HeadlineStacks.RecallFirst);
        return (headline, withoutLlm, recallFirst) switch
        {
            ({ } h, { } without, { } llm) => new ShieldsBadge(1, BadgeLabel, $"{Truncated(llm.Recall)}% with a local LLM · {Truncated(without.Recall)}% without · v{h.Version}", "blue"),
            ({ } h, { } without, null) => new ShieldsBadge(1, BadgeLabel, $"{Truncated(without.Recall)}% · {HeadlineStacks.WithoutLlm} · v{h.Version}", "blue"),
            _ => new ShieldsBadge(1, BadgeLabel, "not measured yet", "lightgrey"),
        };
    }

    // Rounded down to one decimal: 99.96% shows as 99.9%, never as 100.0%.
    private static string Truncated(double rate) => (Math.Floor((decimal)rate * 1000m) / 10m).ToString("0.0", CultureInfo.InvariantCulture);
}
