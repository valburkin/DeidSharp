using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Predictions;

/// <summary>
/// Another tool's predictions, read from a file, scored like a DeidSharp stack. The tool runs
/// in its own environment (deid in Perl, Philter in Python); only its spans come here, so the
/// comparison uses one scorer and none of the tool's code.
/// </summary>
/// <remarks>
/// The format follows the file's extension: <c>.phi</c> is deid's own output
/// (<see cref="DeidPhiPredictions"/>), anything else the tool's TSV (<see cref="TsvPredictions"/>).
/// Documents are named by their id in the corpus ("patient 3, note 2", "query 815"), and
/// offsets count UTF-16 code units, end exclusive, in the text the corpus reader builds. Every
/// span is checked against the corpus before scoring starts; errors name line numbers, never
/// what a line says.
/// </remarks>
internal sealed class PredictionsFile : IScoredStack
{
    private static readonly IPredictionsFormat Tsv = new TsvPredictions();

    // TSV is the format every other tool is converted to, outside this repository. Only a
    // format native to a corpus the tool reads gets a reader and a row here (deid's .phi is
    // PhysioNet's), so readers for every tool someone compares don't pile up.
    private static readonly Dictionary<string, IPredictionsFormat> FormatsByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".phi"] = new DeidPhiPredictions(),
    };

    private readonly Dictionary<string, List<PhiSpan>> _spans;

    private PredictionsFile(string name, Dictionary<string, List<PhiSpan>> spans)
    {
        Name = name;
        _spans = spans;
    }

    public string Name { get; }

    public string? Baseline => null;

    /// <summary>Reads and checks the file against every document of the corpus, before any scoring starts.</summary>
    public static PredictionsFile Read(string name, string path, IReadOnlyList<CorpusDocument> corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        var lengths = corpus.ToDictionary(d => d.Id, d => d.Text.Length, StringComparer.Ordinal);
        var spans = new Dictionary<string, List<PhiSpan>>(StringComparer.Ordinal);
        foreach (var span in FormatsByExtension.GetValueOrDefault(Path.GetExtension(path), Tsv).Read(name, path))
        {
            if (!lengths.TryGetValue(span.Document, out var length))
            {
                throw new UsageException($"--predictions {name}: line {span.LineNumber} names a document the corpus doesn't have.");
            }

            if (span.End <= span.Start || span.End > length)
            {
                throw new UsageException($"--predictions {name}: line {span.LineNumber} has a span that doesn't fit its document.");
            }

            if (!spans.TryGetValue(span.Document, out var list))
            {
                spans[span.Document] = list = [];
            }

            list.Add(new PhiSpan(span.Start, span.End, span.Category, 1.0, name));
        }

        return new PredictionsFile(name, spans);
    }

    public Task<IReadOnlyList<PhiSpan>> PredictAsync(CorpusDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Task.FromResult<IReadOnlyList<PhiSpan>>(_spans.TryGetValue(document.Id, out var spans) ? spans : []);
    }
}
