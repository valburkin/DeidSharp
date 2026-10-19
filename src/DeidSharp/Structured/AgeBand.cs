using System.Globalization;

namespace DeidSharp.Structured;

/// <summary>
/// An age range that replaces an exact age: "60s", "85-89", or the open top band "90+"
/// that Safe Harbor requires for everyone over 89.
/// </summary>
/// <param name="Lower">The youngest age in the band.</param>
/// <param name="Upper">The oldest age in the band; null for the open top band.</param>
public sealed record AgeBand(int Lower, int? Upper)
{
    public bool IsTopCoded => Upper is null;

    /// <summary>
    /// "90+" for the top band, "60s" for a whole decade from 10 up, otherwise "0-9",
    /// "85-89", or "45" for a one-year band.
    /// </summary>
    public string Label => Upper switch
    {
        null => string.Create(CultureInfo.InvariantCulture, $"{Lower}+"),
        { } upper when upper == Lower => Lower.ToString(CultureInfo.InvariantCulture),
        { } upper when Lower >= 10 && Lower % 10 == 0 && upper == Lower + 9 => string.Create(CultureInfo.InvariantCulture, $"{Lower}s"),
        { } upper => string.Create(CultureInfo.InvariantCulture, $"{Lower}-{upper}"),
    };

    public override string ToString() => Label;
}
