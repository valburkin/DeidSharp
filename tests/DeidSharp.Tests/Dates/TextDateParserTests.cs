using DeidSharp.Dates;

namespace DeidSharp.Tests.Dates;

public class TextDateParserTests
{
    [Theory]
    [InlineData("2024-03-05", 2024, 3, 5)]
    [InlineData("2024/3/5", 2024, 3, 5)]
    [InlineData("2024.03.05", 2024, 3, 5)]
    [InlineData("2024-03-05T10:00:00Z", 2024, 3, 5)]
    [InlineData("2024-03-05 14:30", 2024, 3, 5)]
    [InlineData("2024-03-05T10:00:00.123-05:00", 2024, 3, 5)]
    [InlineData("20240305", 2024, 3, 5)]
    [InlineData("3/5/2024", 2024, 3, 5)]
    [InlineData("03-05-2024", 2024, 3, 5)]
    [InlineData("25/12/2023", 2023, 12, 25)]
    [InlineData("25.12.2023", 2023, 12, 25)]
    [InlineData("3/5/24", 2024, 3, 5)]
    [InlineData("3/5/75", 1975, 3, 5)]
    [InlineData("March 5, 2024", 2024, 3, 5)]
    [InlineData("march 5th 2024", 2024, 3, 5)]
    [InlineData("Sept. 7, 2021", 2021, 9, 7)]
    [InlineData("Sep 7 2021", 2021, 9, 7)]
    [InlineData("5 March 2024", 2024, 3, 5)]
    [InlineData("7th of September, 2021", 2021, 9, 7)]
    [InlineData("Feb 29, 2024", 2024, 2, 29)]
    [InlineData("05-Mar-1950", 1950, 3, 5)]
    [InlineData("05MAR1950", 1950, 3, 5)]
    [InlineData("05-Mar-50", 1950, 3, 5)]
    [InlineData("2024-Mar-05", 2024, 3, 5)]
    [InlineData("Mar-05-2024", 2024, 3, 5)]
    [InlineData("  2024-03-05  ", 2024, 3, 5)]
    public void Reads_full_dates(string text, int year, int month, int day)
    {
        Assert.True(TextDateParser.TryParse(text, out var date));
        Assert.Equal(new PartialDate(year, month, day), date);
    }

    [Theory]
    [InlineData("March 2024", 2024, 3)]
    [InlineData("Aug, 2019", 2019, 8)]
    [InlineData("3/2024", 2024, 3)]
    [InlineData("12/1999", 1999, 12)]
    public void Reads_month_and_year(string text, int year, int month)
    {
        Assert.True(TextDateParser.TryParse(text, out var date));
        Assert.Equal(new PartialDate(year, month), date);
        Assert.Equal(DatePrecision.Month, date.Precision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("March 5")]
    [InlineData("2024")]
    [InlineData("2024-13-01")]
    [InlineData("2024-02-30")]
    [InlineData("02/30/2024")]
    [InlineData("13/13/2024")]
    [InlineData("Feb 29, 2023")]
    [InlineData("0/5/2024")]
    [InlineData("13/2024")]
    [InlineData("Marchember 5, 2024")]
    [InlineData("tomorrow")]
    [InlineData("2024-03-05 and 2024-03-06")]
    public void Refuses_what_is_not_a_complete_date(string? text) => Assert.False(TextDateParser.TryParse(text, out _));

    [Theory]
    [InlineData("March 0000")]
    [InlineData("3/0000")]
    [InlineData("March 5, 0000")]
    [InlineData("0000-03-05")]
    public void Refuses_year_zero_instead_of_throwing(string text) => Assert.False(TextDateParser.TryParse(text, out _));

    [Theory]
    [InlineData("Jan", 1)]
    [InlineData("january", 1)]
    [InlineData("SEPT", 9)]
    [InlineData("sept.", 9)]
    [InlineData("Sep", 9)]
    [InlineData("June", 6)]
    [InlineData("Jun", 6)]
    [InlineData("Ma", 0)]
    [InlineData("Marc", 0)]
    [InlineData("Septem", 0)]
    [InlineData("", 0)]
    public void Reads_month_names(string name, int expected) => Assert.Equal(expected, TextDateParser.MonthFromName(name));
}
