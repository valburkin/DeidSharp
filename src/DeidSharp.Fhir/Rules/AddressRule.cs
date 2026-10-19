using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for addresses. It claims every Address and passes the rest on.

/// <summary>
/// Cuts an address down to what Safe Harbor allows: state, country and the three-digit
/// ZIP area ("000" for sparse areas). Street lines, city, district, text and period are
/// dropped. An address with nothing left is removed.
/// </summary>
public sealed class AddressRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Value is not Address address)
        {
            return ValueTask.FromResult(ElementAction.Pass);
        }

        var generalized = new Address
        {
            Use = address.Use,
            Type = address.Type,
            State = address.State,
            Country = address.Country,
            PostalCode = context.Session.Options.ZipCodes.Generalize(address.PostalCode),
        };

        var empty = generalized.State is null && generalized.Country is null && generalized.PostalCode is null;
        return ValueTask.FromResult(empty ? ElementAction.Remove : ElementAction.ReplaceWith(generalized));
    }
}
