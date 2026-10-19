namespace DeidSharp.Evaluation.Cli.Predictions;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: each file format spans come in is one reader. PredictionsFile picks the reader by
// the file's extension and checks every span the same way. The tool's own TSV takes any
// tool's converted output; only a format native to a corpus the tool reads gets a class.

/// <summary>A format other tools' predicted spans come in.</summary>
internal interface IPredictionsFormat
{
    /// <summary>
    /// Every span in the file, with the line it came from. Throws <see cref="UsageException"/>
    /// naming the line number, never what the line says, for a line it can't read.
    /// </summary>
    IEnumerable<PredictedSpan> Read(string name, string path);
}

/// <summary>One predicted span as a file gives it: its document's id in the corpus, <c>[Start, End)</c> and the category.</summary>
internal sealed record PredictedSpan(int LineNumber, string Document, int Start, int End, PhiCategory Category);
