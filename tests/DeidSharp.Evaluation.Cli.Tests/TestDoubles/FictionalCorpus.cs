using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Tests.TestDoubles;

// Design pattern: Test Double (Stub).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
// Here: a corpus of made-up notes with the labels a test chose, so scoring and reports can
// be checked on spans whose features are known, without a real corpus.

/// <summary>A corpus of the fictional documents and labels a test gives it.</summary>
internal sealed class FictionalCorpus(IEnumerable<LabelRule> labels, params CorpusDocument[] documents) : ICorpus
{
    public string Id => "fictional";

    public string Title => "Fictional";

    public LabelMap Labels { get; } = new(labels);

    public IReadOnlyList<CorpusDocument> Read() => documents;
}
