using DeidSharp.Dates;

namespace DeidSharp.Structured;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the Safe Harbor date strategy. Every date keeps its year and loses the rest.

// Design pattern: Singleton.
// The pattern: a class that has exactly one instance, reached through a static member.
// Here: the transformer holds no state, so every caller shares Instance; the private
// constructor keeps it the only one.

/// <summary>Safe Harbor dates: keep the year, drop the month and day.</summary>
public sealed class YearOnlyDateTransformer : IDateTransformer
{
    private YearOnlyDateTransformer()
    {
    }

    public static YearOnlyDateTransformer Instance { get; } = new();

    public PartialDate Transform(PartialDate date, string subjectId) => date.WithPrecision(DatePrecision.Year);
}
