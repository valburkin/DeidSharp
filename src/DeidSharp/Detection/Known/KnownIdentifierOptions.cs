namespace DeidSharp.Detection.Known;

// Design pattern: Parameter Object.
// The pattern: settings that travel together are grouped into one object, instead of
// a long list of separate arguments.
// Here: the detector's tuning. An immutable record with defaults, so a caller changes
// only what it needs ("Default with { MinimumIdentifierLength = 6 }") and the object
// checks its own values.

/// <summary>Tuning for <see cref="KnownIdentifierDetector"/>.</summary>
public sealed record KnownIdentifierOptions
{
    public static KnownIdentifierOptions Default { get; } = new();

    /// <summary>
    /// Identifiers (MRN, phone, account…) with fewer letters and digits than this are not
    /// searched for: a three-digit MRN would match every "123" in a note. Such short
    /// identifiers must be caught by other layers. Default 4.
    /// </summary>
    public int MinimumIdentifierLength { get; init; } = 4;

    /// <summary>
    /// Name parts shorter than this are not searched for on their own (an initial "J"
    /// would match every "J"). The full name is still searched for. Default 2.
    /// </summary>
    public int MinimumNameTokenLength { get; init; } = 2;

    /// <summary>The longest a single known-identifier search may run before it fails. Default 2 seconds.</summary>
    public TimeSpan MatchTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <exception cref="ArgumentOutOfRangeException">A length is below 1.</exception>
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MinimumIdentifierLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MinimumNameTokenLength, 1);
    }
}
