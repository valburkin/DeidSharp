using System.Globalization;
using System.Text.RegularExpressions;

namespace DeidSharp.Detection;

/// <summary>
/// Safe Harbor removes ages over 89 and keeps the rest. An age a detector reports is under 90
/// when its text holds exactly one number and that number is below 90 ("75 yo", "75"); an age
/// with no number ("elderly"), or with several ("75-92"), is kept as PHI.
/// </summary>
public static partial class SafeHarborAges
{
    public static bool IsUnder90(string? text)
    {
        var numbers = Number().Matches(text ?? "");
        return numbers.Count == 1 && int.Parse(numbers[0].ValueSpan, CultureInfo.InvariantCulture) < 90;
    }

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex Number();
}
