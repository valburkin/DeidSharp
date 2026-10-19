using DeidSharp.Structured;

namespace DeidSharp.Tests.Structured;

public class AgeGeneralizerTests
{
    private static readonly AgeGeneralizer SafeHarbor = AgeGeneralizer.SafeHarbor;

    [Theory]
    [InlineData(0, "0-9")]
    [InlineData(9, "0-9")]
    [InlineData(10, "10s")]
    [InlineData(59, "50s")]
    [InlineData(60, "60s")]
    [InlineData(89, "80s")]
    [InlineData(90, "90+")]
    [InlineData(120, "90+")]
    public void Safe_harbor_uses_decades_and_puts_everyone_over_89_in_one_band(int age, string expected) =>
        Assert.Equal(expected, SafeHarbor.Generalize(age).Label);

    [Theory]
    [InlineData(5, 90, 87, "85-89")]
    [InlineData(5, 90, 3, "0-4")]
    [InlineData(10, 85, 84, "80-84")]
    [InlineData(10, 85, 85, "85+")]
    [InlineData(1, 90, 45, "45")]
    [InlineData(7, 90, 88, "84-89")]
    public void Band_width_and_top_code_are_configurable(int width, int topCode, int age, string expected) =>
        Assert.Equal(expected, new AgeGeneralizer(width, topCode).Generalize(age).Label);

    [Fact]
    public void The_top_band_is_open()
    {
        var band = SafeHarbor.Generalize(95);
        Assert.True(band.IsTopCoded);
        Assert.Null(band.Upper);
        Assert.Equal(90, band.Lower);
    }

    [Theory]
    [InlineData("1960-05-03", "2026-05-02", 65)]
    [InlineData("1960-05-03", "2026-05-03", 66)]
    [InlineData("2000-02-29", "2001-02-27", 0)]
    [InlineData("2000-02-29", "2001-02-28", 1)]
    [InlineData("2000-02-29", "2004-02-29", 4)]
    [InlineData("2026-10-15", "2026-10-15", 0)]
    public void Counts_completed_years_including_leap_day_birthdays(string born, string asOf, int expected) =>
        Assert.Equal(expected, AgeGeneralizer.AgeInYears(DateOnly.Parse(born, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(asOf, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Generalizes_a_birth_date_as_of_a_given_day() =>
        Assert.Equal("90+", SafeHarbor.Generalize(new DateOnly(1931, 3, 4), new DateOnly(2026, 10, 15)).Label);

    [Fact]
    public void Rejects_a_birth_date_after_the_reference_date() =>
        Assert.Throws<ArgumentException>(() => AgeGeneralizer.AgeInYears(new DateOnly(2027, 1, 1), new DateOnly(2026, 1, 1)));

    [Fact]
    public void Rejects_negative_ages_and_nonsensical_settings()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SafeHarbor.Generalize(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgeGeneralizer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgeGeneralizer(51));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgeGeneralizer(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgeGeneralizer(10, 151));
    }

    [Theory]
    [InlineData("60s", true)]
    [InlineData(" 60s ", true)]
    [InlineData("0-9", true)]
    [InlineData("90+", true)]
    [InlineData("65", false)]
    [InlineData("95+", false)]
    [InlineData("100s", false)]
    [InlineData("67 years", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Recognizes_its_own_band_labels(string? label, bool expected) => Assert.Equal(expected, SafeHarbor.IsBandLabel(label));

    [Fact]
    public void Every_label_it_produces_is_recognized()
    {
        for (var age = 0; age <= 130; age++)
        {
            Assert.True(SafeHarbor.IsBandLabel(SafeHarbor.Generalize(age).Label));
        }
    }
}
