namespace DeidSharp.Presidio;

/// <summary>
/// Widens a model's span to the whole word or token it cuts into. A model can tag only
/// part of a word ("Oka" of "Okafor") or of a token ("rosa" of
/// "rosa.alvarez@mailbox.example"); replacing just that part would leave the rest
/// readable.
/// </summary>
internal static class SpanWidening
{
    private const string EdgePunctuation = ".,;:!?\"'()[]{}<>";

    /// <summary>
    /// Words (names, places, dates…) widen over letters and digits. Identifiers written
    /// as one unbroken token widen to the whole token, without its edge punctuation.
    /// </summary>
    public static (int Start, int End) Widen(string text, int start, int end, PhiCategory category)
    {
        Func<char, bool> inside = IsWordCategory(category) ? char.IsLetterOrDigit : c => !char.IsWhiteSpace(c);

        while (start > 0 && inside(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && inside(text[end]))
        {
            end++;
        }

        while (end - start > 1 && EdgePunctuation.Contains(text[end - 1], StringComparison.Ordinal))
        {
            end--;
        }

        while (end - start > 1 && EdgePunctuation.Contains(text[start], StringComparison.Ordinal))
        {
            start++;
        }

        return (start, end);
    }

    private static bool IsWordCategory(PhiCategory category) =>
        category is PhiCategory.Name or PhiCategory.Location or PhiCategory.Organization
            or PhiCategory.StreetAddress or PhiCategory.Date or PhiCategory.Age or PhiCategory.ZipCode;
}
