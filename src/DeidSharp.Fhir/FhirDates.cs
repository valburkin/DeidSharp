using System.Globalization;
using System.Text.RegularExpressions;
using DeidSharp.Dates;

namespace DeidSharp.Fhir;

/// <summary>Reads FHIR date and dateTime strings ("1960", "1960-05", "1960-05-03", "1960-05-03T10:00:00Z").</summary>
internal static partial class FhirDates
{
    /// <param name="timestamp">The exact time, when the value has one; null for a date alone.</param>
    public static bool TryParse(string? value, out PartialDate date, out DateTimeOffset? timestamp)
    {
        date = default;
        timestamp = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var m = DatePattern().Match(trimmed);
        if (!m.Success || !PartialDate.TryCreate(Int(m.Groups["y"]) ?? 0, Int(m.Groups["m"]), Int(m.Groups["d"]), out date))
        {
            return false;
        }

        if (!m.Groups["t"].Success)
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            date = default;
            return false;
        }

        timestamp = parsed;
        return true;
    }

    private static int? Int(Group group) => group.Success ? int.Parse(group.Value, CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"^(?<y>\d{4})(?:-(?<m>\d{2})(?:-(?<d>\d{2})(?<t>T.+)?)?)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex DatePattern();
}
