using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using DeidSharp.Text;

namespace DeidSharp.Detection.Known;

/// <summary>
/// Builds the regular expression that finds one known identifier in folded text
/// (<see cref="TextFolding.Fold"/>), in the ways people write it: a name in full or
/// by any single part, a date in any common format, a phone or MRN with any
/// punctuation between its digits, a street suffix spelled out or abbreviated.
///
/// Each pattern comes with its anchors (see <see cref="KnownValuePattern"/>): literals of
/// which every match contains at least one, so <see cref="AnchorIndex"/> can skip the
/// search on a text that holds none of them.
/// </summary>
internal static class KnownValuePatterns
{
    // Not part of anyone's name: honorifics, suffixes and credentials.
    private static readonly FrozenSet<string> NameStopTokens = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "mr", "mrs", "ms", "mx", "miss", "dr", "prof", "jr", "sr", "ii", "iii", "iv", "v",
        "md", "phd", "rn", "np", "dds", "esq", "the", "and", "of");

    // Real surnames or name particles that are also everyday words ("Do", "Van", "De",
    // "La"): searched for alone only after a title ("Mr. Do"), or they would redact
    // every "do not" in a note. The full name is always searched for.
    private static readonly FrozenSet<string> AmbiguousNameTokens = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "do", "pa", "de", "la", "le", "da", "di", "du", "van", "von", "der", "den", "del", "dos", "das", "al", "el", "bin", "ben");

    private const string AfterTitle = @"(?<=(?:mr|mrs|ms|mx|miss|dr|prof)\.?\s+)";

    // Street suffixes and their usual abbreviations; either form matches the other.
    private static readonly FrozenDictionary<string, string[]> StreetSuffixes = BuildSuffixes(
        ["street", "st"], ["avenue", "ave", "av"], ["road", "rd"], ["boulevard", "blvd"], ["lane", "ln"],
        ["drive", "dr"], ["court", "ct"], ["place", "pl"], ["terrace", "ter"], ["circle", "cir"],
        ["parkway", "pkwy"], ["highway", "hwy"], ["square", "sq"], ["trail", "trl"], ["apartment", "apt"],
        ["suite", "ste"], ["north", "n"], ["south", "s"], ["east", "e"], ["west", "w"], ["mount", "mt"],
        ["fort", "ft"], ["saint", "st"], ["building", "bldg"], ["floor", "fl"], ["room", "rm"]);

    private const string Before = @"(?<![\p{L}\p{N}])";
    private const string After = @"(?![\p{L}\p{N}])";
    // IsDigitGapSeparator holds the same characters; a test keeps the two in step.
    private const string DigitGap = @"[\s.\-/()]{0,3}";

    /// <summary>
    /// True for the characters <c>DigitGap</c> allows between the characters of a phone
    /// number or identifier. Dropping them from a text puts those characters next to each
    /// other, which is what the compact anchor view relies on.
    /// </summary>
    public static bool IsDigitGapSeparator(char c) => char.IsWhiteSpace(c) || c is '.' or '-' or '/' or '(' or ')';

    // Between the words of a place, organization or street: spaces and the punctuation
    // that sits between words ("C++ (Main) Clinic", "Springfield, MA").
    private const string WordGap = @"[\s,;:()""/#]+";

    /// <summary>The pattern for one identifier, or null when it is too short to search for safely.</summary>
    public static KnownValuePattern? Build(KnownIdentifier identifier, KnownIdentifierOptions options) => identifier.Category switch
    {
        PhiCategory.Name => NamePattern(identifier.Value, options),
        PhiCategory.Date => DatePattern(identifier.Value) ?? PhrasePattern(identifier.Value),
        PhiCategory.StreetAddress or PhiCategory.Location or PhiCategory.Organization => PhrasePattern(identifier.Value),
        PhiCategory.Email or PhiCategory.Url => LiteralPattern(identifier.Value),
        PhiCategory.Age => null,
        PhiCategory.Phone or PhiCategory.Fax => PhonePattern(identifier.Value, options),
        _ => IdentifierPattern(identifier.Value, options),
    };

    // Every alternative holds one word of the name literally: the full name its first
    // word, a part the part itself. Those words are the anchors.
    private static KnownValuePattern? NamePattern(string name, KnownIdentifierOptions options)
    {
        var folded = TextFolding.FoldValue(name);
        var words = Words(folded);
        if (words.Count == 0)
        {
            return null;
        }

        var alternatives = new List<string>();
        var anchors = new List<string>();
        if (words.Count > 1)
        {
            alternatives.Add(string.Join(@"[\s,]+", words.Select(Regex.Escape)));
            anchors.Add(words[0]);
        }

        foreach (var word in words)
        {
            var parts = word.Split('-', StringSplitOptions.RemoveEmptyEntries).Prepend(word).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var part in parts)
            {
                var bare = part.Trim('\'');
                if (bare.Length < options.MinimumNameTokenLength || NameStopTokens.Contains(bare))
                {
                    continue;
                }

                alternatives.Add(AmbiguousNameTokens.Contains(bare) ? AfterTitle + Regex.Escape(part) : Regex.Escape(part));
                anchors.Add(part);
            }
        }

        return Alternation(alternatives) is { } pattern ? new KnownValuePattern(pattern, AnchorView.Folded, anchors) : null;
    }

    // Every format writes the year as a whole number: in full, as two digits, or inside
    // the compact yyyyMMdd. Those numbers are the anchors.
    private static KnownValuePattern? DatePattern(string value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        var y = date.Year.ToString("D4", CultureInfo.InvariantCulture);
        var yy = (date.Year % 100).ToString("D2", CultureInfo.InvariantCulture);
        var m = $"0?{date.Month}";
        var d = $"0?{date.Day}";
        var mm = date.Month.ToString("D2", CultureInfo.InvariantCulture);
        var dd = date.Day.ToString("D2", CultureInfo.InvariantCulture);
        var monthName = MonthNamePattern(date.Month);
        const string ordinal = "(?:st|nd|rd|th)?";

        string[] formats =
        [
            $"{y}[-/.]{m}[-/.]{d}",
            $"{y}{mm}{dd}",
            $@"{m}(?<us>[-/.]){d}\k<us>(?:{y}|{yy})",
            $@"{d}(?<eu>[-/.]){m}\k<eu>(?:{y}|{yy})",
            $@"{monthName}\.?(?:\s+|[-/.]\s*){d}{ordinal}(?:,?\s+{y}|[-/.]\s*(?:{y}|{yy}))",
            $@"{d}{ordinal}(?:\s+(?:of\s+)?|[-/.]\s*|){monthName}\.?(?:,?\s+{y}|[-/.]?\s*{y}|[-/.]?{yy})",
            $@"{y}[-/.\s]{monthName}\.?[-/.\s]{d}",
        ];

        return new KnownValuePattern(
            @"(?<!\d)(?:" + string.Join("|", formats) + @")(?!\d)",
            AnchorView.DigitRun,
            [y, yy, y + mm + dd]);
    }

    private static string MonthNamePattern(int month)
    {
        var full = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
        var abbr = full[..3];
        return month == 9 ? $"(?:{full}|sept|{abbr})" : $"(?:{full}|{abbr})";
    }

    // A street address, place or organization name, word by word, so "St. Brendan
    // Hospital" also matches "Saint Brendan Hospital" and "St Brendan Hospital". A match
    // holds every word, so any one would do as the anchor; the rarest-looking one is taken
    // (with letters, and the longest at its shortest spelling) so the search runs on as
    // few texts as possible: "Brendan", not "St" or the house number of "742 Evergreen Terrace".
    private static KnownValuePattern? PhrasePattern(string value)
    {
        var words = Words(TextFolding.FoldValue(value));
        if (words.Count == 0)
        {
            return null;
        }

        var anchor = words
            .OrderByDescending(w => w.Any(char.IsLetter))
            .ThenByDescending(w => Spellings(w).Min(s => s.Length))
            .First();
        return new KnownValuePattern(Before + string.Join(WordGap, words.Select(WordPattern)) + After, AnchorView.Folded, Spellings(anchor));
    }

    // One word, in any of its usual spellings (Street/St, Saint/St), with or without a
    // trailing period.
    private static string WordPattern(string word) =>
        (StreetSuffixes.TryGetValue(word, out var forms)
            ? "(?:" + string.Join("|", forms.Select(Regex.Escape)) + ")"
            : Regex.Escape(word)) + @"\.?";

    private static string[] Spellings(string word) => StreetSuffixes.TryGetValue(word, out var forms) ? forms : [word];

    private static KnownValuePattern LiteralPattern(string value)
    {
        var folded = TextFolding.FoldValue(value.Trim());
        return new KnownValuePattern(@"(?<![\w.@%+\-])" + Regex.Escape(folded) + @"(?![\w@\-])", AnchorView.Folded, [folded]);
    }

    // The digits, with any separators between them dropped, are the anchor.
    private static KnownValuePattern? PhonePattern(string value, KnownIdentifierOptions options)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        if (digits.Length < options.MinimumIdentifierLength)
        {
            return null;
        }

        // An optional "+1"/"1" country code in front, so "16175550198" is found too.
        return new KnownValuePattern(
            Before + @"(?:\+?1[\s.\-]?)?" + string.Join(DigitGap, digits.Select(c => c.ToString())) + After,
            AnchorView.Compact,
            [digits]);
    }

    // The characters, with any separators between them dropped, are the anchor.
    private static KnownValuePattern? IdentifierPattern(string value, KnownIdentifierOptions options)
    {
        var characters = new string(TextFolding.FoldValue(value).Where(char.IsAsciiLetterOrDigit).ToArray());
        return characters.Length < options.MinimumIdentifierLength
            ? null
            : new KnownValuePattern(SeparatedPattern(characters), AnchorView.Compact, [characters]);
    }

    // The characters in order, allowing up to three separators (space, dot, dash,
    // slash, parentheses) between any two: "555-123-4567" matches "(555) 123 4567".
    private static string SeparatedPattern(string characters) =>
        Before + string.Join(DigitGap, characters.Select(c => Regex.Escape(c.ToString()))) + After;

    private static List<string> Words(string value) =>
        [.. value
            .Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ';', ':', '(', ')', '"'))
            .Where(w => w.Length > 0)];

    private static string? Alternation(List<string> alternatives)
    {
        if (alternatives.Count == 0)
        {
            return null;
        }

        // Longest first, so the full name wins over one of its parts at the same position.
        var ordered = alternatives.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(a => a.Length);
        return Before + "(?:" + string.Join("|", ordered) + ")" + After;
    }

    private static FrozenDictionary<string, string[]> BuildSuffixes(params string[][] groups)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var form in group)
            {
                if (!map.TryGetValue(form, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[form] = set;
                }

                set.UnionWith(group);
            }
        }

        return map.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.Order(StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
    }
}
