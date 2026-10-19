using DeidSharp.Pseudonymization;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Pseudonymization;

public class DateShifterTests
{
    private static readonly DateShifter Shifter = new(new HmacPseudonymizer(TestKeys.Fixed), maxDays: 30);

    [Fact]
    public void Every_subject_gets_a_stable_nonzero_offset_within_range()
    {
        for (var i = 0; i < 2000; i++)
        {
            var subject = $"patient-{i}";
            var offset = Shifter.OffsetDays(subject);
            Assert.NotEqual(0, offset);
            Assert.InRange(Math.Abs(offset), 1, 30);
            Assert.Equal(offset, Shifter.OffsetDays(subject));
        }
    }

    [Fact]
    public void Offsets_go_both_ways()
    {
        var offsets = Enumerable.Range(0, 200).Select(i => Shifter.OffsetDays($"s{i}")).ToList();
        Assert.Contains(offsets, o => o < 0);
        Assert.Contains(offsets, o => o > 0);
    }

    [Fact]
    public void A_timestamp_keeps_its_time_of_day_and_offset()
    {
        var original = new DateTimeOffset(2024, 3, 5, 10, 30, 15, TimeSpan.FromHours(-5));
        var shifted = Shifter.Shift(original, "p");
        Assert.Equal(original.TimeOfDay, shifted.TimeOfDay);
        Assert.Equal(original.Offset, shifted.Offset);
        Assert.Equal(Shifter.OffsetDays("p"), (shifted - original).TotalDays);
    }

    [Fact]
    public void Shifting_at_the_ends_of_time_clamps_instead_of_returning_the_original()
    {
        var subject = Enumerable.Range(0, 100).Select(i => $"s{i}").First(s => Shifter.OffsetDays(s) < 0);
        Assert.Equal(DateOnly.MinValue, Shifter.Shift(DateOnly.MinValue, subject));
        var nearStart = DateTimeOffset.MinValue.AddDays(2);
        Assert.NotEqual(nearStart, Shifter.Shift(nearStart, subject));

        var forward = Enumerable.Range(0, 100).Select(i => $"s{i}").First(s => Shifter.OffsetDays(s) > 0);
        Assert.Equal(DateOnly.MaxValue, Shifter.Shift(DateOnly.MaxValue, forward));
        var nearEnd = DateTimeOffset.MaxValue.AddDays(-2);
        Assert.NotEqual(nearEnd, Shifter.Shift(nearEnd, forward));
    }

    [Fact]
    public void Rejects_a_nonsensical_range()
    {
        var pseudonymizer = new HmacPseudonymizer(TestKeys.Fixed);
        Assert.Throws<ArgumentOutOfRangeException>(() => new DateShifter(pseudonymizer, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DateShifter(pseudonymizer, 3651));
        Assert.Throws<ArgumentNullException>(() => new DateShifter(null!));
    }
}
