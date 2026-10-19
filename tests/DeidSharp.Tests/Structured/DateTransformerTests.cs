using DeidSharp.Dates;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Structured;

public class DateTransformerTests
{
    private static readonly DateShifter Shifter = new(new HmacPseudonymizer(TestKeys.Fixed));
    private static readonly ShiftingDateTransformer Shifting = new(Shifter);

    [Theory]
    [InlineData(2024, 3, 5)]
    [InlineData(2024, 3, null)]
    [InlineData(2024, null, null)]
    public void Year_only_keeps_the_year_at_any_precision(int year, int? month, int? day) =>
        Assert.Equal(new PartialDate(year), YearOnlyDateTransformer.Instance.Transform(new PartialDate(year, month, day), "p"));

    [Fact]
    public void Shifting_moves_a_day_by_the_subjects_offset()
    {
        var shifted = Shifting.Transform(new PartialDate(2024, 3, 5), "p");
        var expected = new DateOnly(2024, 3, 5).AddDays(Shifter.OffsetDays("p"));
        Assert.Equal(PartialDate.FromDate(expected), shifted);
    }

    [Fact]
    public void Shifting_keeps_the_precision_it_was_given()
    {
        Assert.Equal(DatePrecision.Month, Shifting.Transform(new PartialDate(2024, 3), "p").Precision);
        Assert.Equal(DatePrecision.Year, Shifting.Transform(new PartialDate(2024), "p").Precision);
    }

    [Fact]
    public void Shifting_preserves_intervals_within_a_subject()
    {
        var admitted = Shifting.Transform(new PartialDate(2024, 3, 2), "p").Midpoint();
        var discharged = Shifting.Transform(new PartialDate(2024, 3, 9), "p").Midpoint();
        Assert.Equal(7, discharged.DayNumber - admitted.DayNumber);
    }

    [Fact]
    public void Shifting_rejects_a_missing_shifter() => Assert.Throws<ArgumentNullException>(() => new ShiftingDateTransformer(null!));
}
