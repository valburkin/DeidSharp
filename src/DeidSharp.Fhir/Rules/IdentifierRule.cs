using Hl7.Fhir.Model;
using DeidSharp.Pseudonymization;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for business identifiers. It passes the rest on.

/// <summary>
/// Business identifiers (MRN, SSN, plan number, NPI…). Safe Harbor removes them.
/// Pseudonymization keeps the identifier's system and type but replaces the value with
/// a keyed pseudonym, so the same MRN gets the same pseudonym in every run with the key.
/// The assigner and period are dropped either way.
/// </summary>
public sealed class IdentifierRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Value is not Identifier identifier)
        {
            return ValueTask.FromResult(ElementAction.Pass);
        }

        if (context.Session.Mode == FhirDeidentificationMode.SafeHarbor || string.IsNullOrWhiteSpace(identifier.Value))
        {
            return ValueTask.FromResult(ElementAction.Remove);
        }

        var digest = context.Session.Pseudonymizer.Digest("fhir-identifier:" + (identifier.System ?? string.Empty), identifier.Value.Trim());
        var pseudonymized = new Identifier
        {
            Use = identifier.Use,
            Type = identifier.Type?.DeepCopy(),
            System = identifier.System,
            Value = PseudonymFormats.Hex(digest),
        };

        context.Session.Statistics.IdsPseudonymized++;
        return ValueTask.FromResult(ElementAction.ReplaceWith(pseudonymized));
    }
}
