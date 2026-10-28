using System.Text;
using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Reporting;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one ICorpusOutput, the one --gold-review picks: the gold spans in context,
// for a person. The run writes whichever output the command line chose.

/// <summary>
/// Writes every gold span with its label in its context, document by document, for a person
/// to check a corpus's annotation: for a generated corpus, whether each surrogate has the
/// kind its context needs. Holds corpus text: refused inside a Git working tree (<see cref="ReviewFiles"/>).
/// </summary>
internal sealed class GoldReviewFile : ICorpusOutput
{
    public const string Option = "gold-review";

    /// <param name="path">The file to write, outside any Git working tree.</param>
    public GoldReviewFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ReviewFiles.EnsureOutsideRepository(path, Option);
        Destination = path;
    }

    public string Destination { get; }

    public async Task WriteAsync(IReadOnlyList<CorpusDocument> documents, TextWriter console)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(console);
        Write(Destination, documents);
        foreach (var (rule, spans) in CountByRule(documents))
        {
            await console.WriteLineAsync(Formats.Invariant($"{spans,8:N0}  {rule}"));
        }

        await console.WriteLineAsync(Formats.Invariant($"Wrote the {documents.Sum(d => d.Spans.Count):N0} gold spans of {documents.Count:N0} documents to {Path.GetFullPath(Destination)}. It holds corpus text: keep it out of Git."));
    }

    /// <summary>How many gold spans each rule typed, most first, for a generated corpus: counts only, for the console.</summary>
    public static IEnumerable<(string Rule, int Spans)> CountByRule(IEnumerable<CorpusDocument> documents) =>
        documents.SelectMany(d => d.Spans).Where(s => s.TypedBy is not null)
            .GroupBy(s => s.TypedBy!, StringComparer.Ordinal)
            .Select(g => (Rule: g.Key, Spans: g.Count()))
            .OrderByDescending(r => r.Spans).ThenBy(r => r.Rule, StringComparer.Ordinal);

    public static void Write(string path, IReadOnlyList<CorpusDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ReviewFiles.EnsureOutsideRepository(path, Option);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var document in documents)
        {
            writer.Write($"## {document.Id}: {document.Spans.Count} gold spans\n\n");
            foreach (var span in document.Spans)
            {
                writer.Write($"{span.Label}\t{span.TypedBy}\t{ReviewFiles.InContext(document.Text, span.Start, span.End)}\n");
            }

            writer.Write('\n');
        }
    }
}
