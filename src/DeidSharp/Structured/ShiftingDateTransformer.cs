using DeidSharp.Dates;
using DeidSharp.Pseudonymization;

namespace DeidSharp.Structured;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the pseudonymized date strategy. Each subject's dates move together by that
// subject's secret offset, so intervals between them survive.

/// <summary>
/// Pseudonymized dates: every date of a subject moves by that subject's offset
/// (<see cref="DateShifter"/>), at the precision it had. A partial date is shifted from
/// its midpoint, so "2024-03" moves as the 15th of March would.
/// </summary>
public sealed class ShiftingDateTransformer(DateShifter shifter) : IDateTransformer
{
    private readonly DateShifter _shifter = shifter ?? throw new ArgumentNullException(nameof(shifter));

    public PartialDate Transform(PartialDate date, string subjectId)
    {
        var shifted = PartialDate.FromDate(_shifter.Shift(date.Midpoint(), subjectId));
        return shifted.WithPrecision(date.Precision);
    }
}
