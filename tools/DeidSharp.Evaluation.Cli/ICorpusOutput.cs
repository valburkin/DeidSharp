using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: each way of writing the corpus out instead of measuring it (--export for other
// tools, --gold-review for a person) is one ICorpusOutput; the run calls whichever the
// command line chose, without knowing which.

/// <summary>
/// Writes a run's documents out for another tool or a person to read, instead of measuring
/// anything. The output holds corpus text, so each one refuses a place inside a Git working tree.
/// </summary>
internal interface ICorpusOutput
{
    /// <summary>Writes the documents, and to <paramref name="console"/> only counts and where they went.</summary>
    Task WriteAsync(IReadOnlyList<CorpusDocument> documents, TextWriter console);
}
