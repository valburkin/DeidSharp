namespace DeidSharp.Pseudonymization;

/// <summary>
/// Moves every date of one subject by the same secret number of days, so intervals
/// (length of stay, days since diagnosis, age differences) survive while the real
/// dates do not. Each subject gets its own offset, derived from the key; it is never
/// zero.
///
/// Date shifting is pseudonymization, not Safe Harbor: the shifted dates are still
/// dates. Use it under an expert determination or a data use agreement.
/// </summary>
public sealed class DateShifter
{
    private const string Domain = "date-shift";

    private readonly IPseudonymizer _pseudonymizer;

    /// <param name="maxDays">The largest shift in either direction (1–3650). Default 365.</param>
    public DateShifter(IPseudonymizer pseudonymizer, int maxDays = 365)
    {
        ArgumentNullException.ThrowIfNull(pseudonymizer);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDays, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxDays, 3650);
        _pseudonymizer = pseudonymizer;
        MaxDays = maxDays;
    }

    public int MaxDays { get; }

    /// <summary>The subject's offset in days: in [-MaxDays, -1] or [1, MaxDays].</summary>
    public int OffsetDays(string subjectId)
    {
        ArgumentNullException.ThrowIfNull(subjectId);
        var digest = _pseudonymizer.Digest(Domain, subjectId);
        var magnitude = (int)PseudonymFormats.Index(digest, MaxDays) + 1;
        return (digest[^1] & 1) == 0 ? magnitude : -magnitude;
    }

    /// <summary>Shifts a date, clamped to the representable range.</summary>
    public DateOnly Shift(DateOnly date, string subjectId)
    {
        var shifted = date.DayNumber + (long)OffsetDays(subjectId);
        return DateOnly.FromDayNumber((int)Math.Clamp(shifted, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber));
    }

    /// <summary>
    /// Shifts a timestamp by whole days, keeping its time of day and UTC offset. Near the
    /// ends of the representable range it clamps instead of returning the original.
    /// </summary>
    public DateTimeOffset Shift(DateTimeOffset timestamp, string subjectId)
    {
        // Keep 14 hours of headroom so the local time stays representable at any offset.
        var headroom = TimeSpan.FromHours(14).Ticks;
        var ticks = timestamp.UtcTicks + OffsetDays(subjectId) * TimeSpan.TicksPerDay;
        var clamped = Math.Clamp(ticks, DateTime.MinValue.Ticks + headroom, DateTime.MaxValue.Ticks - headroom);
        return new DateTimeOffset(clamped, TimeSpan.Zero).ToOffset(timestamp.Offset);
    }
}
