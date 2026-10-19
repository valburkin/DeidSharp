namespace DeidSharp.Structured;

/// <summary>
/// Replaces an exact age or birth date with an <see cref="AgeBand"/>. Ages at or above
/// the top-code age (90 under Safe Harbor) all fall into one open band, because very
/// old people are few enough to be identifiable by age alone.
/// </summary>
public sealed class AgeGeneralizer
{
    /// <summary>Safe Harbor: ten-year bands, everyone 90 and over in "90+".</summary>
    public static AgeGeneralizer SafeHarbor { get; } = new();

    /// <param name="bandWidth">Years per band (1–50). Default 10.</param>
    /// <param name="topCodeAge">The age from which everyone shares one band. Safe Harbor requires 90 or lower. Default 90.</param>
    public AgeGeneralizer(int bandWidth = 10, int topCodeAge = 90)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bandWidth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bandWidth, 50);
        ArgumentOutOfRangeException.ThrowIfLessThan(topCodeAge, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(topCodeAge, 150);
        BandWidth = bandWidth;
        TopCodeAge = topCodeAge;
    }

    public int BandWidth { get; }

    public int TopCodeAge { get; }

    public AgeBand Generalize(int ageYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ageYears);
        if (ageYears >= TopCodeAge)
        {
            return new AgeBand(TopCodeAge, null);
        }

        var lower = ageYears / BandWidth * BandWidth;
        var upper = Math.Min(lower + BandWidth - 1, TopCodeAge - 1);
        return new AgeBand(lower, upper);
    }

    /// <summary>The band for someone born on <paramref name="birthDate"/>, as of <paramref name="asOf"/>.</summary>
    public AgeBand Generalize(DateOnly birthDate, DateOnly asOf) => Generalize(AgeInYears(birthDate, asOf));

    /// <summary>
    /// Completed years between two dates. Someone born on 29 February turns a year older
    /// on 28 February in non-leap years (the .NET <see cref="DateOnly.AddYears"/> rule).
    /// </summary>
    public static int AgeInYears(DateOnly birthDate, DateOnly asOf)
    {
        if (birthDate > asOf)
        {
            throw new ArgumentException("The birth date is after the reference date.", nameof(birthDate));
        }

        var age = asOf.Year - birthDate.Year;
        return asOf < birthDate.AddYears(age) ? age - 1 : age;
    }

    /// <summary>True if <paramref name="label"/> is a band this generalizer can produce.</summary>
    public bool IsBandLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var trimmed = label.Trim();
        for (var lower = 0; lower < TopCodeAge; lower += BandWidth)
        {
            if (Generalize(lower).Label == trimmed)
            {
                return true;
            }
        }

        return Generalize(TopCodeAge).Label == trimmed;
    }
}
