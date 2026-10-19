using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each removed element is one row (the parent it sits in, its name). Removing
// another element is another row, not another branch.

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the first handler in the default chain. It removes the elements in its table
// before any other rule can look inside them.

/// <summary>
/// Removes specific elements that are identifying in their own right: photos, contact
/// people, device serial numbers, coordinates, site names, transaction details. Each
/// row is one element; add rows (or a rule) rather than branches.
/// </summary>
public sealed class RemovedElementsRule : IFhirElementRule
{
    private static readonly RemovedElement[] Rows =
    [
        // Photos are full-face images (Safe Harbor Q); contacts are relatives' details.
        new(p => p is Patient or RelatedPerson or Practitioner or Person, "photo"),
        new(p => p is Patient, "contact"),

        // Device identifiers and serial numbers (Safe Harbor M).
        new(p => p is Device, "serialNumber"),
        new(p => p is Device, "lotNumber"),
        new(p => p is Device, "udiCarrier"),
        new(p => p is Device, "distinctIdentifier"),

        // Health plan and authorization numbers kept as plain strings (Safe Harbor I, R).
        new(p => p is Coverage, "subscriberId"),
        new(p => p is Coverage, "dependent"),
        new(p => p is Coverage, "class"),
        new(p => p is Claim.InsuranceComponent, "preAuthRef"),
        new(p => p is ExplanationOfBenefit.InsuranceComponent, "preAuthRef"),

        // Site names and coordinates narrow the location below state level.
        new(p => p is Organization, "name"),
        new(p => p is Organization, "alias"),
        new(p => p is Location, "name"),
        new(p => p is Location, "alias"),
        new(p => p is Location, "description"),
        new(p => p is Location, "position"),

        // A note's author written as a name.
        new(p => p is Annotation, "author"),

        // Raw binary content cannot be inspected.
        new(p => p is Binary, "data"),

        // Where the record came from (host names, server URLs).
        new(p => p is Meta, "source"),

        // Transaction and search bookkeeping: conditional headers carry identifiers,
        // links carry server URLs with search parameters.
        new(p => p is Bundle, "link"),
        new(p => p is Bundle, "signature"),
        new(p => p is Bundle.EntryComponent, "link"),
        new(p => p is Bundle.EntryComponent, "search"),
        new(p => p is Bundle.EntryComponent, "response"),
        new(p => p is Bundle.RequestComponent, "ifNoneExist"),
        new(p => p is Bundle.RequestComponent, "ifMatch"),
        new(p => p is Bundle.RequestComponent, "ifNoneMatch"),
        new(p => p is Bundle.RequestComponent, "ifModifiedSince"),
    ];

    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Annotation.author is a choice: a Reference to a person is rewritten like any
        // reference; only a name written as a string is removed.
        if (context is { Parent: Annotation, ElementName: "author", Value: ResourceReference })
        {
            return ValueTask.FromResult(ElementAction.Pass);
        }

        var removed = Rows.Any(row => row.Element == context.ElementName && row.Parent(context.Parent));
        return ValueTask.FromResult(removed ? ElementAction.Remove : ElementAction.Pass);
    }

    private sealed record RemovedElement(Func<Base, bool> Parent, string Element);
}
