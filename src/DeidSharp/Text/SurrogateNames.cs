using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using DeidSharp.Detection;
using DeidSharp.Pseudonymization;

namespace DeidSharp.Text;

/// <summary>
/// Fake names for <see cref="SurrogateReplacement"/>. Each part of a name maps to its own
/// fake, keyed by that part alone, so "Rosa Maria Alvarez-Nunez", "Mrs. Alvarez-Nunez" and
/// "Rosa" stay one consistent person. The last part of a multi-part name, or a lone part
/// that ends one of the subject's known names, draws a surname; any other part draws a
/// given name. Initials stay initials, and a name in capitals gets a fake in capitals.
/// </summary>
internal sealed class SurrogateNames(IPseudonymizer pseudonymizer)
{
    private static readonly char[] WordEdges = ['.', ',', ';', ':', '(', ')', '"', '\''];

    // Design pattern: Memoization (a cache keyed by the input).
    // The pattern: the result of an expensive computation is stored against its input,
    // so the same input never pays for it twice.
    // Here: the surnames of an identifier set, needed for every one-word name replaced.
    // Sets are immutable, so the surnames never go stale, and the weak table lets a set
    // and its surnames be collected together.
    private static readonly ConditionalWeakTable<KnownIdentifiers, FrozenSet<string>> Surnames = [];

    /// <param name="attempt">Which try this is; each try draws a different fake.</param>
    public string Generate(string original, TextContext context, int attempt)
    {
        var words = WhiteSpace.Split(original);
        var knownSurnames = words.Length == 1 ? Surnames.GetValue(context.Known, KnownSurnames) : FrozenSet<string>.Empty;
        var fakes = words.Select((word, index) =>
        {
            var isSurname = words.Length > 1 ? index == words.Length - 1 : knownSurnames.Contains(NormalizeWord(word.Trim(WordEdges)));
            return FakeWord(word, isSurname, context.SubjectId, attempt);
        });

        var name = string.Join(' ', fakes);
        return IsAllCapitals(original) ? name.ToUpperInvariant() : name;
    }

    /// <summary>True if the two names share any part longer than an initial.</summary>
    public static bool SharePart(string original, string candidate)
    {
        var originalParts = WhiteSpace.Split(original).Select(NormalizeWord).ToHashSet(StringComparer.Ordinal);
        return WhiteSpace.Split(candidate).Select(NormalizeWord).Any(part => part.Length > 1 && originalParts.Contains(part));
    }

    // One word of a name, its punctuation kept: "Lee's" → "Carter's", "J." → "M.".
    private string FakeWord(string word, bool isSurname, string subjectId, int attempt)
    {
        var core = word.Trim(WordEdges);
        if (core.Length == 0)
        {
            return word;
        }

        var domain = string.Create(CultureInfo.InvariantCulture, $"surrogate:Name:{(isSurname ? "last" : "first")}:{subjectId}:{attempt}");
        var digest = pseudonymizer.Digest(domain, NormalizeWord(core));
        var fake = SurrogateValues.Pick(isSurname ? SurrogateData.LastNames : SurrogateData.FirstNames, digest, 0);
        return word.Replace(core, core.Length == 1 ? fake[..1] : fake, StringComparison.Ordinal);
    }

    private static FrozenSet<string> KnownSurnames(KnownIdentifiers known) =>
        known.Items
            .Where(item => item.Category == PhiCategory.Name)
            .SelectMany(item => WhiteSpace.Split(item.Value).TakeLast(1))
            .Select(NormalizeWord)
            .ToFrozenSet(StringComparer.Ordinal);

    private static bool IsAllCapitals(string text)
    {
        var letters = text.Where(char.IsLetter).ToList();
        return letters.Count > 1 && letters.All(char.IsUpper);
    }

    private static string NormalizeWord(string word) => TextFolding.FoldValue(word).Trim('.', ',', '\'').ToUpperInvariant();
}
