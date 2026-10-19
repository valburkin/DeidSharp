using System.Collections.Frozen;
using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for ages, and for quantities measured in years. It passes the
// rest on.

/// <summary>
/// Ages. An Age value (e.g. <c>Condition.onsetAge</c>) at or over the top-code age, in
/// any unit, becomes "≥ 90 years". So does any other quantity measured in years at or
/// over it, such as an "Age" observation's <c>valueQuantity</c>: nothing else is
/// recorded in years at that size. Younger ages are kept, as a clean copy without
/// extensions. An Age whose unit cannot be read is removed.
/// </summary>
public sealed class AgeRule : IFhirElementRule
{
    private const string Ucum = "http://unitsofmeasure.org";

    // UCUM's "a" and the ways people write years, upper-cased.
    private static readonly FrozenSet<string> YearUnits =
        FrozenSet.Create(StringComparer.Ordinal, "A", "YR", "YRS", "YEAR", "YEARS", "Y");

    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var topCode = context.Session.Options.TopCodeAge;
        return ValueTask.FromResult(context.Value switch
        {
            Age age => AgeValue(age, topCode),
            Quantity quantity when IsYears(quantity) && quantity.Value >= topCode => ElementAction.ReplaceWith(TopCoded(quantity, topCode)),
            _ => ElementAction.Pass,
        });
    }

    private static ElementAction AgeValue(Age age, int topCode)
    {
        if (age.Value is null)
        {
            return ElementAction.ReplaceWith(CleanCopy.Of(age));
        }

        return InYears(age) switch
        {
            // An age whose unit cannot be read cannot be checked; drop it.
            null => ElementAction.Remove,
            { } years when years < topCode => ElementAction.ReplaceWith(CleanCopy.Of(age)),
            _ => ElementAction.ReplaceWith(TopCoded(age, topCode)),
        };
    }

    // A copy keeps the original's type (Age, Duration, Quantity), so it fits the element;
    // every one of its values is then overwritten.
    private static Quantity TopCoded(Quantity original, int topCode)
    {
        var result = CleanCopy.Of(original);
        result.Value = topCode;
        result.Comparator = Quantity.QuantityComparator.GreaterOrEqual;
        result.Unit = "years";
        result.System = Ucum;
        result.Code = "a";
        return result;
    }

    private static bool IsYears(Quantity quantity) => Unit(quantity) is { } unit && YearUnits.Contains(unit);

    private static string? Unit(Quantity quantity) => (quantity.Code ?? quantity.Unit)?.Trim().ToUpperInvariant();

    private static decimal? InYears(Age age) => Unit(age) switch
    {
        { } unit when YearUnits.Contains(unit) => age.Value,
        "MO" or "MONTH" or "MONTHS" => age.Value / 12m,
        "WK" or "WEEK" or "WEEKS" => age.Value / 52m,
        "D" or "DAY" or "DAYS" => age.Value / 365m,
        "H" or "HOUR" or "HOURS" or "MIN" or "MINUTE" or "MINUTES" => 0m,
        _ => null,
    };
}
