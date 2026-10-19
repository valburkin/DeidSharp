using System.Globalization;
using System.Text.RegularExpressions;

namespace DeidSharp.Dates;

/// <summary>
/// Reads a date written the way clinical notes write dates: ISO ("2024-03-05"), numeric
/// ("3/5/2024", "05.03.24"), with a month name ("March 5th, 2024", "5 Mar 2024",
/// "05-Mar-2024", "05MAR1950", "2024-Mar-05"), month and year ("March 2024", "3/2024")
/// or compact ("20240305"). A date without a year ("March 5") is not parsed: there is
/// nothing to keep or shift.
///
/// Numeric dates are read month-first (US) and fall back to day-first when month-first
/// is impossible ("25/03/2024"). Two-digit years use the invariant calendar's pivot
/// (00–49 → 2000s, 50–99 → 1900s).
/// </summary>
public static partial class TextDateParser
{
    public static bool TryParse(string? text, out PartialDate date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        return TryIso(value, out date)
            || TryCompact(value, out date)
            || TryNumeric(value, out date)
            || TryMonthName(value, out date)
            || TryNumericMonthYear(value, out date);
    }

    /// <summary>
    /// Reads a month name or abbreviation ("Mar", "march", "Sept."); returns 0 if it is not
    /// one, which <see cref="PartialDate.TryCreate"/> rejects as a month.
    /// </summary>
    public static int MonthFromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var key = name.Trim().TrimEnd('.').ToUpperInvariant();
        if (key.Length < 3)
        {
            return 0;
        }

        for (var month = 1; month <= 12; month++)
        {
            var full = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month).ToUpperInvariant();
            if (full == key || (full.StartsWith(key, StringComparison.Ordinal) && (key.Length == 3 || key == "SEPT")))
            {
                return month;
            }
        }

        return 0;
    }

    private static bool TryIso(string value, out PartialDate date) => TryYearMonthDay(IsoPattern().Match(value), out date);

    private static bool TryCompact(string value, out PartialDate date) => TryYearMonthDay(CompactPattern().Match(value), out date);

    private static bool TryNumeric(string value, out PartialDate date)
    {
        date = default;
        var m = NumericPattern().Match(value);
        if (!m.Success)
        {
            return false;
        }

        var year = FourDigitYear(m.Groups["y"].Value);
        var a = Int(m.Groups["a"].Value);
        var b = Int(m.Groups["b"].Value);
        return PartialDate.TryCreate(year, a, b, out date) || PartialDate.TryCreate(year, b, a, out date);
    }

    private static bool TryMonthName(string value, out PartialDate date)
    {
        var m = MonthDayYearPattern().Match(value);
        if (!m.Success)
        {
            m = DayMonthYearPattern().Match(value);
        }

        if (!m.Success)
        {
            m = YearMonthDayPattern().Match(value);
        }

        if (m.Success)
        {
            return PartialDate.TryCreate(FourDigitYear(m.Groups["y"].Value), MonthFromName(m.Groups["mon"].Value), Int(m.Groups["d"].Value), out date);
        }

        m = MonthYearPattern().Match(value);
        date = default;
        return m.Success && PartialDate.TryCreate(Int(m.Groups["y"].Value), MonthFromName(m.Groups["mon"].Value), null, out date);
    }

    private static bool TryNumericMonthYear(string value, out PartialDate date)
    {
        var m = NumericMonthYearPattern().Match(value);
        date = default;
        return m.Success && PartialDate.TryCreate(Int(m.Groups["y"].Value), Int(m.Groups["m"].Value), null, out date);
    }

    // A match with "y", "m" and "d" groups, all numeric.
    private static bool TryYearMonthDay(Match m, out PartialDate date)
    {
        date = default;
        return m.Success && PartialDate.TryCreate(Int(m.Groups["y"].Value), Int(m.Groups["m"].Value), Int(m.Groups["d"].Value), out date);
    }

    private static int FourDigitYear(string y) =>
        y.Length == 2 ? CultureInfo.InvariantCulture.Calendar.ToFourDigitYear(Int(y)) : Int(y);

    private static int Int(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    private const string MonthName = @"(?<mon>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?";

    [GeneratedRegex(@"^(?<y>\d{4})[-/.](?<m>\d{1,2})[-/.](?<d>\d{1,2})(?:[T ]\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+\-]\d{2}:?\d{2})?)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex IsoPattern();

    [GeneratedRegex(@"^(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex CompactPattern();

    [GeneratedRegex(@"^(?<a>\d{1,2})(?<s>[-/.])(?<b>\d{1,2})\k<s>(?<y>\d{4}|\d{2})$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NumericPattern();

    [GeneratedRegex("^" + MonthName + @"(?:\s+|[\-/.]\s*)(?<d>\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(?<y>\d{4})|[\-/.]\s*(?<y>\d{4}|\d{2}))$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex MonthDayYearPattern();

    [GeneratedRegex(@"^(?<d>\d{1,2})(?:st|nd|rd|th)?(?:\s+(?:of\s+)?|[\-/.]\s*|(?=[A-Za-z]))" + MonthName + @"(?:,?\s+(?<y>\d{4})|[\-/.]?\s*(?<y>\d{4})|[\-/.](?<y>\d{2}))$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex DayMonthYearPattern();

    [GeneratedRegex(@"^(?<y>\d{4})[\-/.\s]" + MonthName + @"[\-/.\s](?<d>\d{1,2})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex YearMonthDayPattern();

    [GeneratedRegex("^" + MonthName + @",?\s+(?<y>\d{4})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex MonthYearPattern();

    [GeneratedRegex(@"^(?<m>\d{1,2})/(?<y>\d{4})$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NumericMonthYearPattern();
}
