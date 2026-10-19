using DeidSharp.Dates;

namespace DeidSharp.Structured;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: what happens to a date depends on the de-identification mode. Safe Harbor keeps
// the year only; pseudonymization shifts the date per subject. Every place that
// handles a date (FHIR elements, dates in notes) calls the one strategy it was given.

/// <summary>Transforms a date that belongs to a subject (a patient).</summary>
public interface IDateTransformer
{
    /// <summary>The transformed date. Its precision is never finer than the input's.</summary>
    PartialDate Transform(PartialDate date, string subjectId);
}
