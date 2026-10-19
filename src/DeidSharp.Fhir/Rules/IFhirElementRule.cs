namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility.
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: every FHIR element is offered to the rules in order (names, addresses,
// identifiers, dates, free text…). The first rule that does not Pass decides; an
// element no rule claims is descended into. New handling is a new rule in the list
// (FhirDeidentificationOptions.AdditionalRules), not an edit to the walker.

/// <summary>
/// Decides what happens to one FHIR element during de-identification. Free text is
/// queued (<see cref="FhirDeidentificationSession.QueueText"/>) and rewritten after the
/// walk, so a string or markdown value seen during the walk still holds the original text.
/// </summary>
public interface IFhirElementRule
{
    ValueTask<ElementAction> ApplyAsync(FhirElementContext context);
}
