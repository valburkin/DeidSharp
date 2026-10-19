using DeidSharp.Dates;

namespace DeidSharp.Tests.Dates;

public class PartialDateTests
{
    [Fact]
    public void Rejects_impossible_dates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(10000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(2024, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(2024, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(2023, 2, 29));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(2024, 4, 31));
        Assert.Throws<ArgumentException>(() => new PartialDate(2024, null, 5));
    }

    [Theory]
    [InlineData(2024, null, null, DatePrecision.Year, "2024")]
    [InlineData(2024, 3, null, DatePrecision.Month, "2024-03")]
    [InlineData(2024, 3, 5, DatePrecision.Day, "2024-03-05")]
    [InlineData(42, 1, 9, DatePrecision.Day, "0042-01-09")]
    public void Knows_its_precision_and_writes_iso_at_that_precision(int year, int? month, int? day, DatePrecision precision, string iso)
    {
        var date = new PartialDate(year, month, day);
        Assert.Equal(precision, date.Precision);
        Assert.Equal(iso, date.ToString());
    }

    [Fact]
    public void The_midpoint_is_the_day_itself_the_15th_or_1_july()
    {
        Assert.Equal(new DateOnly(2024, 3, 5), new PartialDate(2024, 3, 5).Midpoint());
        Assert.Equal(new DateOnly(2024, 3, 15), new PartialDate(2024, 3).Midpoint());
        Assert.Equal(new DateOnly(2024, 7, 1), new PartialDate(2024).Midpoint());
    }

    [Fact]
    public void Precision_can_be_reduced_but_never_invented()
    {
        var day = new PartialDate(2024, 3, 5);
        Assert.Equal(new PartialDate(2024, 3), day.WithPrecision(DatePrecision.Month));
        Assert.Equal(new PartialDate(2024), day.WithPrecision(DatePrecision.Year));
        Assert.Equal(new PartialDate(2024), new PartialDate(2024).WithPrecision(DatePrecision.Day));
        Assert.Equal(new PartialDate(2024, 3), new PartialDate(2024, 3).WithPrecision(DatePrecision.Day));
    }

    [Theory]
    [InlineData(2024, null, null, true)]
    [InlineData(2024, 2, 29, true)]
    [InlineData(2023, 2, 29, false)]
    [InlineData(0, null, null, false)]
    [InlineData(10000, null, null, false)]
    [InlineData(2024, 0, null, false)]
    [InlineData(2024, 13, null, false)]
    [InlineData(2024, null, 5, false)]
    public void TryCreate_accepts_exactly_what_the_constructor_accepts(int year, int? month, int? day, bool valid)
    {
        Assert.Equal(valid, PartialDate.TryCreate(year, month, day, out var date));
        if (valid)
        {
            Assert.Equal(new PartialDate(year, month, day), date);
        }
        else
        {
            Assert.ThrowsAny<ArgumentException>(() => new PartialDate(year, month, day));
        }
    }

    [Fact]
    public void The_first_and_last_day_bound_the_period()
    {
        Assert.Equal(new DateOnly(2024, 1, 1), new PartialDate(2024).FirstDay());
        Assert.Equal(new DateOnly(2024, 12, 31), new PartialDate(2024).LastDay());
        Assert.Equal(new DateOnly(2024, 2, 1), new PartialDate(2024, 2).FirstDay());
        Assert.Equal(new DateOnly(2024, 2, 29), new PartialDate(2024, 2).LastDay());
        Assert.Equal(new DateOnly(2024, 2, 5), new PartialDate(2024, 2, 5).FirstDay());
        Assert.Equal(new DateOnly(2024, 2, 5), new PartialDate(2024, 2, 5).LastDay());
    }

    [Fact]
    public void FromDate_keeps_every_part() =>
        Assert.Equal(new PartialDate(1999, 12, 31), PartialDate.FromDate(new DateOnly(1999, 12, 31)));
}
