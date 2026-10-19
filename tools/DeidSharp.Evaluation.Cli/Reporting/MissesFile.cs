using System.Text;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// Writes every missed gold span in its context, for a person to read and decide what to
/// change. Holds corpus text: refused inside a Git working tree (<see cref="ReviewFiles"/>).
/// </summary>
internal static class MissesFile
{
    public const string Option = "misses";

    public static void Write(string path, IEnumerable<StackResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        ReviewFiles.EnsureOutsideRepository(path, Option);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var result in results)
        {
            writer.Write($"## {result.Stack}: {result.Misses.Count} missed\n\n");
            foreach (var ((id, text, _, _), span) in result.Misses)
            {
                writer.Write($"{id}\t[{span.Start}, {span.End})\t{span.Label}\t{ReviewFiles.InContext(text, span.Start, span.End)}\n");
            }

            writer.Write('\n');
        }
    }
}
