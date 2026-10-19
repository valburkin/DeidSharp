using System.Globalization;

namespace DeidSharp.Text;

/// <summary>
/// Folds text for matching without changing its length: accented Latin letters become
/// their base letter ("José" → "Jose"), typographic apostrophes and dashes become ASCII,
/// and Unicode spaces become a plain space. Because every character maps to exactly one
/// character, an index found in the folded text is the same index in the original.
///
/// The table is written out rather than computed with <see cref="string.Normalize()"/>,
/// which is not available everywhere in globalization-invariant mode.
/// </summary>
public static class TextFolding
{
    // U+00C0..U+017F, one replacement per code point (Latin-1 Supplement letters, then
    // Latin Extended-A). × and ÷ map to themselves.
    private const string LatinTable =
        "AAAAAAACEEEEIIII" + "DNOOOOO×OUUUUYTs" + "aaaaaaaceeeeiiii" + "dnooooo÷ouuuuyty" +
        "AaAaAaCcCcCcCcDd" + "DdEeEeEeEeEeGgGg" + "GgGgHhHhIiIiIiIi" + "IiIiJjKkkLlLlLlL" +
        "lLlNnNnNnnNnOoOo" + "OoOoRrRrRrSsSsSs" + "SsTtTtTtUuUuUuUu" + "UuUuWwYyYZzZzZzs";

    private const char LatinTableStart = 'À';

    private const char LatinTableEnd = 'ſ';

    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = FoldChar(source[i]);
            }
        });
    }

    /// <summary>
    /// Folds a value that will be searched for (a known name, an address line): the same
    /// folding as <see cref="Fold"/>, minus any combining marks, so a value stored in
    /// decomposed form ("e" + U+0301) still matches the folded text.
    /// </summary>
    public static string FoldValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Concat(Fold(value).Where(c => !IsCombiningMark(c)));
    }

    internal static bool IsCombiningMark(char c) => CharUnicodeInfo.GetUnicodeCategory(c) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    // Invisible and look-alike characters are written as escapes, so the table can be read.
    private static char FoldChar(char c) => c switch
    {
        < '\u0080' => c,
        >= LatinTableStart and <= LatinTableEnd => LatinTable[c - LatinTableStart],

        // Romanian S and T with comma below (Latin Extended-B).
        '\u0218' => 'S',
        '\u0219' => 's',
        '\u021A' => 'T',
        '\u021B' => 't',

        // Typographic apostrophes, the acute accent and the prime.
        '\u2018' or '\u2019' or '\u201B' or '\u02BC' or '\u00B4' or '\u2032' => '\'',

        // Hyphens, dashes and the minus sign.
        '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2015' or '\u2212' => '-',

        // No-break, narrow, mathematical, ideographic and typographic spaces.
        '\u00A0' or '\u202F' or '\u205F' or '\u3000' => ' ',
        >= '\u2000' and <= '\u200A' => ' ',
        _ => c,
    };
}
