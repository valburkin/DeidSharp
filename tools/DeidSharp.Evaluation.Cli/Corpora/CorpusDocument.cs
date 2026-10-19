namespace DeidSharp.Evaluation.Cli.Corpora;

/// <summary>One annotated text as its corpus ships it, with the corpus's own labels.</summary>
/// <param name="Id">Where the document sits in its corpus ("patient 3, note 2"), never what it says.</param>
/// <param name="Group">The documents that must stay on one side of a split ("patient 3"); null for the document alone.</param>
internal sealed record CorpusDocument(string Id, string Text, IReadOnlyList<LabeledSpan> Spans, string? Group = null)
{
    public string SplitGroup => Group ?? Id;
}

/// <summary>A gold span <c>[Start, End)</c> with the corpus's own label ("HCPName", "NAME").</summary>
internal sealed record LabeledSpan(int Start, int End, string Label);
