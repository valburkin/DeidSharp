namespace DeidSharp.Gazetteer;

/// <summary>Tuning for <see cref="GazetteerDetector"/>.</summary>
public sealed record GazetteerOptions
{
    public static GazetteerOptions Default { get; } = new();

    /// <summary>
    /// A surname counts on its own only if at least this many people had it in the 2010
    /// Census: rarer surnames collide with abbreviations and clinical words, above all in
    /// capitals. A rarer surname still counts with context ("Dr. Xu"). Default 10,000 (3,567
    /// surnames), chosen on the tuning half of PhysioNet's nursing notes
    /// (docs/evaluation/findings.md, F9).
    /// </summary>
    public int MinimumSurnameCount { get; init; } = 10_000;

    /// <summary>
    /// Also looks up words written in capitals ("HART"), as many clinical notes write names and
    /// places. Capitals are also how notes write abbreviations, so the common-word and clinical-term
    /// lists matter more. Default true: half the names and places in PhysioNet's nursing
    /// notes are written in capitals.
    /// </summary>
    public bool Capitals { get; init; } = true;

    /// <summary>Also looks up words written in lowercase ("hart"). Default false: it finds more, but flags far more ordinary text.</summary>
    public bool Lowercase { get; init; }

    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfNegative(MinimumSurnameCount);
}
