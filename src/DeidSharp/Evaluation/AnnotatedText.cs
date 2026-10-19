namespace DeidSharp.Evaluation;

/// <summary>A text with its PHI marked by a human (the gold standard), for measuring a detector.</summary>
public sealed record AnnotatedText(string Text, IReadOnlyList<GoldSpan> Gold)
{
    public string Text { get; } = Text ?? throw new ArgumentNullException(nameof(Text));

    public IReadOnlyList<GoldSpan> Gold { get; } = Validate(Text, Gold);

    private static IReadOnlyList<GoldSpan> Validate(string text, IReadOnlyList<GoldSpan> gold)
    {
        ArgumentNullException.ThrowIfNull(gold);
        foreach (var span in gold)
        {
            if (span is null || span.Start < 0 || span.End <= span.Start || span.End > text.Length)
            {
                throw new ArgumentException($"Gold span {span} does not fit text of length {text.Length}.", nameof(gold));
            }
        }

        return gold;
    }
}

/// <summary>One annotated identifier: <c>[Start, End)</c> and its category.</summary>
public sealed record GoldSpan(int Start, int End, PhiCategory Category);
