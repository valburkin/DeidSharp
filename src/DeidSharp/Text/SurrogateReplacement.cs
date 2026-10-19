using System.Globalization;
using DeidSharp.Dates;
using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;

namespace DeidSharp.Text;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the surrogate strategy. Every span becomes a realistic fake of its kind, so an
// identifier a detector missed hides among fakes instead of standing out among tags.

/// <summary>
/// Replaces each span with a realistic fake of the same kind ("hiding in plain sight"):
/// a name with a name, an MRN with an MRN-shaped number, a date with the transformed
/// date. If a detector misses an identifier, the real one then sits among fakes and is
/// much harder to pick out than in text where every other identifier is a "[TAG]".
///
/// Surrogates are deterministic: the same original under the same key and subject
/// always becomes the same fake, so "Mr. Lee … Lee's daughter" stays consistent. A
/// surrogate never equals one of the subject's own known identifiers. Fakes use reserved
/// ranges where they exist (555-01xx phones, example.org, 192.0.2.x, SSN area 000).
/// </summary>
public sealed class SurrogateReplacement : IReplacementStrategy
{
    private const int Attempts = 8;

    private readonly IPseudonymizer _pseudonymizer;
    private readonly IDateTransformer _dates;
    private readonly ZipCodeGeneralizer _zipCodes;
    private readonly AgeGeneralizer _ages;
    private readonly SurrogateNames _names;
    private readonly KnownIdentifierDetector _knownDetector = new();
    private readonly TagReplacement _fallback = new();

    /// <param name="dates">How dates in text are rewritten: year only (Safe Harbor) or shifted.</param>
    /// <param name="zipCodes">How ZIP codes are cut down. Default: Safe Harbor.</param>
    /// <param name="ages">Which ages are hidden: those at or over the generalizer's top-code age. Default: Safe Harbor (90).</param>
    public SurrogateReplacement(
        IPseudonymizer pseudonymizer, IDateTransformer dates, ZipCodeGeneralizer? zipCodes = null, AgeGeneralizer? ages = null)
    {
        _pseudonymizer = pseudonymizer ?? throw new ArgumentNullException(nameof(pseudonymizer));
        _dates = dates ?? throw new ArgumentNullException(nameof(dates));
        _zipCodes = zipCodes ?? ZipCodeGeneralizer.SafeHarbor;
        _ages = ages ?? AgeGeneralizer.SafeHarbor;
        _names = new SurrogateNames(pseudonymizer);
    }

    public string Replace(PhiSpan span, string original, TextContext context)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(context);

        return span.Category switch
        {
            PhiCategory.Date => DateSurrogate(original, context),
            PhiCategory.Age => AgeSurrogate(original),
            PhiCategory.ZipCode => _zipCodes.Generalize(original) is { } prefix ? prefix + "XX" : _fallback.Tag(span.Category),
            PhiCategory.Biometric or PhiCategory.Photo => _fallback.Tag(span.Category),
            _ => AvoidingKnown(span.Category, original, context),
        };
    }

    private string DateSurrogate(string original, TextContext context) =>
        TextDateParser.TryParse(original, out var date)
            ? _dates.Transform(date, context.SubjectId).ToString()
            : _fallback.Tag(PhiCategory.Date);

    // Safe Harbor only requires ages over 89 to be hidden: a younger age is clinical
    // content and stays. An age that is not a plain number cannot be judged, so it gets a tag.
    private string AgeSurrogate(string original) =>
        int.TryParse(original.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var years)
            ? years >= _ages.TopCodeAge ? _ages.Generalize(years).Label : original
            : _fallback.Tag(PhiCategory.Age);

    private string AvoidingKnown(PhiCategory category, string original, TextContext context)
    {
        var normalized = Normalize(category, original);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var candidate = Candidate(category, original, normalized, context, attempt);
            if (!Repeats(category, original, normalized, candidate) && !IsKnown(candidate, context))
            {
                return candidate;
            }
        }

        // Every attempt collided with a known identifier (vanishingly unlikely): a tag
        // is always safe.
        return _fallback.Tag(category);
    }

    private string Candidate(PhiCategory category, string original, string normalized, TextContext context, int attempt)
    {
        if (category == PhiCategory.Name)
        {
            return _names.Generate(original, context, attempt);
        }

        var domain = string.Create(CultureInfo.InvariantCulture, $"surrogate:{category}:{context.SubjectId}:{attempt}");
        return SurrogateValues.Generate(category, original, _pseudonymizer.Digest(domain, normalized));
    }

    private bool IsKnown(string candidate, TextContext context) =>
        context.Known.Count > 0 && _knownDetector.Detect(new DetectionRequest(candidate, context.Known)).Count > 0;

    // A fake must never be the original again, whole or (for names) in any part: drawn
    // from a short list, "Mark Pierce" could otherwise map to itself.
    private static bool Repeats(PhiCategory category, string original, string normalized, string candidate) =>
        Normalize(category, candidate) == normalized
        || (category == PhiCategory.Name && SurrogateNames.SharePart(original, candidate));

    // Two spellings of the same identifier get the same surrogate: case, accents,
    // spacing and (for numbers) punctuation don't matter.
    private static string Normalize(PhiCategory category, string original)
    {
        var folded = TextFolding.FoldValue(original).ToUpperInvariant();
        return category is PhiCategory.Name or PhiCategory.Location or PhiCategory.Organization
            or PhiCategory.StreetAddress or PhiCategory.Email or PhiCategory.Url
            ? string.Join(' ', WhiteSpace.Split(folded))
            : string.Concat(folded.Where(char.IsAsciiLetterOrDigit));
    }
}
