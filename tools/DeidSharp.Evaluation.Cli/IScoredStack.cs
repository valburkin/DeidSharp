using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the scorer asks each stack for a document's spans, and doesn't know whether a
// DeidSharp detector stack found them or another tool did and wrote them to a file.

/// <summary>What the scorer measures: anything that predicts PHI spans for a corpus document.</summary>
internal interface IScoredStack
{
    /// <summary>The name the stack is reported under ("deterministic + clinical", "deid").</summary>
    string Name { get; }

    /// <summary>The stack this one changes ("deterministic + clinical" for its gazetteer variant), if any.</summary>
    string? Baseline { get; }

    Task<IReadOnlyList<PhiSpan>> PredictAsync(CorpusDocument document, CancellationToken cancellationToken);
}
