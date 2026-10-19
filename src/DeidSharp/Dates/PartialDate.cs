using System.Globalization;

namespace DeidSharp.Dates;

/// <summary>
/// A calendar date known to year, month or day precision ("1960", "1960-05",
/// "1960-05-03"), as clinical records and FHIR store them.
/// </summary>
public readonly record struct PartialDate
{
    public PartialDate(int year, int? month = null, int? day = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        if (day is not null && month is null)
        {
            throw new ArgumentException("A day needs a month.", nameof(day));
        }

        if (month is { } m)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(m, 1, nameof(month));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(m, 12, nameof(month));
            if (day is { } d)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(d, 1, nameof(day));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(d, DateTime.DaysInMonth(year, m), nameof(day));
            }
        }

        Year = year;
        Month = month;
        Day = day;
    }

    public int Year { get; }

    public int? Month { get; }

    public int? Day { get; }

    public DatePrecision Precision => Day is not null ? DatePrecision.Day : Month is not null ? DatePrecision.Month : DatePrecision.Year;

    public static PartialDate FromDate(DateOnly date) => new(date.Year, date.Month, date.Day);

    /// <summary>
    /// Creates the date if its parts make a real one (year 1–9999, a month for any day,
    /// a day that exists in its month); returns false instead of throwing otherwise.
    /// </summary>
    public static bool TryCreate(int year, int? month, int? day, out PartialDate date)
    {
        var valid = year is >= 1 and <= 9999
            && month is null or >= 1 and <= 12
            && (day is null || (month is { } m && day >= 1 && day <= DateTime.DaysInMonth(year, m)));
        date = valid ? new PartialDate(year, month, day) : default;
        return valid;
    }

    /// <summary>
    /// The day that best represents this date for arithmetic: the day itself, the 15th
    /// of a month, or 1 July of a year. Shifting the midpoint keeps a shifted partial
    /// date inside a plausible range instead of always rounding toward the period start.
    /// </summary>
    public DateOnly Midpoint() => Precision switch
    {
        DatePrecision.Day => new DateOnly(Year, Month!.Value, Day!.Value),
        DatePrecision.Month => new DateOnly(Year, Month!.Value, 15),
        _ => new DateOnly(Year, 7, 1),
    };

    /// <summary>The earliest day this date allows: the day itself, the 1st of the month, or 1 January.</summary>
    public DateOnly FirstDay() => new(Year, Month ?? 1, Day ?? 1);

    /// <summary>The latest day this date allows: the day itself, the last of the month, or 31 December.</summary>
    public DateOnly LastDay() => Precision switch
    {
        DatePrecision.Day => new DateOnly(Year, Month!.Value, Day!.Value),
        DatePrecision.Month => new DateOnly(Year, Month!.Value, DateTime.DaysInMonth(Year, Month.Value)),
        _ => new DateOnly(Year, 12, 31),
    };

    /// <summary>Returns the date reduced to <paramref name="precision"/> (never increased).</summary>
    public PartialDate WithPrecision(DatePrecision precision) =>
        precision >= Precision ? this
        : precision == DatePrecision.Month ? new PartialDate(Year, Month)
        : new PartialDate(Year);

    /// <summary>ISO 8601 at this date's precision: "yyyy", "yyyy-MM" or "yyyy-MM-dd".</summary>
    public override string ToString() => Precision switch
    {
        DatePrecision.Day => string.Create(CultureInfo.InvariantCulture, $"{Year:D4}-{Month:D2}-{Day:D2}"),
        DatePrecision.Month => string.Create(CultureInfo.InvariantCulture, $"{Year:D4}-{Month:D2}"),
        _ => Year.ToString("D4", CultureInfo.InvariantCulture),
    };
}
