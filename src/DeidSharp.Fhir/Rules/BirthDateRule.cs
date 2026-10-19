using Hl7.Fhir.Model;
using DeidSharp.Structured;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for the birth dates of people 90 or over. Every other birth date
// passes on to DateRule.

/// <summary>
/// Safe Harbor's age rule: for anyone aged 90 or over (the top-code age), even the
/// birth year must go, because it reveals the age. The age is taken at death if the
/// record has a death date, otherwise at the reference date, and is computed from the
/// earliest day the birth date allows, so a year-only birth date errs toward removal.
/// Younger birth dates are left to <see cref="DateRule"/>.
/// </summary>
public sealed class BirthDateRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not { ElementName: "birthDate", Parent: Patient or RelatedPerson or Person or Practitioner })
        {
            return ValueTask.FromResult(ElementAction.Pass);
        }

        if (context.Value is not Date date || !FhirDates.TryParse(date.Value, out var birth, out _))
        {
            // Unreadable birth date: nothing can be kept safely.
            return ValueTask.FromResult(ElementAction.Remove);
        }

        var asOf = DeathDate(context.Parent) ?? context.Session.ReferenceDate;
        var earliestBirth = birth.FirstDay();
        if (earliestBirth > asOf)
        {
            return ValueTask.FromResult(ElementAction.Pass);
        }

        return ValueTask.FromResult(AgeGeneralizer.AgeInYears(earliestBirth, asOf) >= context.Session.Options.TopCodeAge
            ? ElementAction.Remove
            : ElementAction.Pass);
    }

    // The latest day the death date allows, so the age errs high.
    private static DateOnly? DeathDate(Base parent) =>
        parent is Patient { Deceased: FhirDateTime deceased } && FhirDates.TryParse(deceased.Value, out var death, out _)
            ? death.LastDay()
            : null;
}
