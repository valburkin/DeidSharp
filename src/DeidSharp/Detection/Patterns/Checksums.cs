namespace DeidSharp.Detection.Patterns;

/// <summary>Check-digit algorithms that tell a real identifier from a number that only looks like one.</summary>
public static class Checksums
{
    /// <summary>The Luhn (mod 10) check used by payment card numbers. Non-digits are ignored.</summary>
    public static bool IsValidLuhn(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sum = 0;
        var count = 0;
        var doubleIt = false;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            var c = value[i];
            if (!char.IsAsciiDigit(c))
            {
                continue;
            }

            var digit = c - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            count++;
            doubleIt = !doubleIt;
        }

        return count >= 2 && sum % 10 == 0;
    }

    /// <summary>
    /// The North American VIN check digit (position 9, ISO 3779 / 49 CFR 565). A
    /// 17-character string with I, O or Q, or a wrong check digit, is not a valid VIN.
    /// </summary>
    public static bool IsValidVin(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 17)
        {
            return false;
        }

        ReadOnlySpan<int> weights = [8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum = 0;
        for (var i = 0; i < 17; i++)
        {
            var v = VinValue(char.ToUpperInvariant(value[i]));
            if (v < 0)
            {
                return false;
            }

            sum += v * weights[i];
        }

        var remainder = sum % 11;
        var expected = remainder == 10 ? 'X' : (char)('0' + remainder);
        return char.ToUpperInvariant(value[8]) == expected;
    }

    private static int VinValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        'A' or 'J' => 1,
        'B' or 'K' or 'S' => 2,
        'C' or 'L' or 'T' => 3,
        'D' or 'M' or 'U' => 4,
        'E' or 'N' or 'V' => 5,
        'F' or 'W' => 6,
        'G' or 'P' or 'X' => 7,
        'H' or 'Y' => 8,
        'R' or 'Z' => 9,
        _ => -1,
    };
}
