using System.Text;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// Writes every predicted span that is not gold PHI in its context, grouped by category,
/// with the detectors that reported it: what to read before tuning a detector for precision.
/// Holds corpus text: refused inside a Git working tree (<see cref="ReviewFiles"/>).
/// </summary>
internal static class FalsePositivesFile
{
    public const string Option = "false-positives";

    public static void Write(string path, IEnumerable<StackResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        ReviewFiles.EnsureOutsideRepository(path, Option);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var result in results)
        {
            writer.Write($"## {result.Stack}: {result.FalsePositives.Count} false positives\n\n");
            var ordered = result.FalsePositives
                .OrderBy(f => f.Span.Category)
                .ThenBy(f => Sources(f.Span), StringComparer.Ordinal);
            foreach (var ((id, text, _, _), span) in ordered)
            {
                writer.Write($"{id}\t[{span.Start}, {span.End})\t{span.Category}\t{Sources(span)}\t{ReviewFiles.InContext(text, span.Start, span.End)}\n");
            }

            writer.Write('\n');
        }
    }

    private static string Sources(PhiSpan span) => string.Join(",", span.Sources);
}
