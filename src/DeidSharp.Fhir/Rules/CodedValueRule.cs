using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for codings and quantities. It sits before FreeTextRule, so
// vocabulary is never run through name detection.

/// <summary>
/// Terminology and measurements: a Coding (system, code, display from a code system)
/// and a Quantity (value and unit) are kept, but as a clean copy without extensions or
/// element ids. Their strings are vocabulary, not free text, and sending "Hodgkin
/// lymphoma" through name detection would only damage it; an extension, though, can
/// hold anything, and is not vocabulary.
/// </summary>
public sealed class CodedValueRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.Value is Coding or Quantity
            ? ElementAction.ReplaceWith(CleanCopy.Of(context.Value))
            : ElementAction.Pass);
    }
}
