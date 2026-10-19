using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for data types that exist to identify someone. It runs early, so
// nothing inside them is processed.

/// <summary>
/// Removes, wherever they appear, the data types that exist to identify someone:
/// human names, contact points (phone, fax, email, URL) and signatures, plus narrative
/// (the human-readable HTML of a resource, generated from the very fields being
/// removed, and too free-form to clean reliably).
/// </summary>
public sealed class RemovedTypesRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.Value is HumanName or ContactPoint or Signature or Narrative
            ? ElementAction.Remove
            : ElementAction.Pass);
    }
}
