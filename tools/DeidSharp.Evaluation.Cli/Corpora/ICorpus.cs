namespace DeidSharp.Evaluation.Cli.Corpora;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: each corpus file format is one reader. The scorer runs whichever corpus the
// command line names, so a new corpus is a new class and a row in CorpusCatalog.

/// <summary>An annotated corpus in its own file format.</summary>
internal interface ICorpus
{
    /// <summary>The corpus's id on the command line and in the results file ("physionet-deid").</summary>
    string Id { get; }

    /// <summary>The corpus's full name, for reports.</summary>
    string Title { get; }

    /// <summary>What each of the corpus's labels means in DeidSharp's categories.</summary>
    LabelMap Labels { get; }

    /// <summary>
    /// Reads every document and checks its gold spans against its text. Throws
    /// <see cref="CorpusFormatException"/> rather than score a corpus that doesn't line up.
    /// </summary>
    IReadOnlyList<CorpusDocument> Read();
}
