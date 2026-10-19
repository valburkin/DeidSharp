namespace DeidSharp.Tests.Text;

/// <summary>Checks on UTF-16 text that tests share.</summary>
internal static class Utf16
{
    /// <summary>True if any half of a surrogate pair stands without its other half.</summary>
    public static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return true;
                }

                i++;
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }
}
