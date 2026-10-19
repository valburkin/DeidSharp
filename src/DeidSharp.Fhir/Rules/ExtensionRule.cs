using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for extensions. It removes those not allowed, descends into the
// rest, and passes every other element on.

/// <summary>
/// Extensions are kept only if their URL is allowed (fail-closed: an unknown extension
/// can hold anything, e.g. a mother's maiden name or a birthplace). A kept extension is
/// descended into, so its values still pass through the other rules, and its
/// sub-extensions (relative URLs such as "ombCategory") are kept with it.
///
/// Removing a modifier extension can change what a resource means, so each one removed
/// is counted in the statistics for review.
/// </summary>
public sealed class ExtensionRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Value is not Extension extension)
        {
            return ValueTask.FromResult(ElementAction.Pass);
        }

        var allowed = context.Parent is Extension
            || (extension.Url is { } url && context.Session.Options.AllowedExtensions.Contains(url));
        if (allowed)
        {
            return ValueTask.FromResult(ElementAction.Descend);
        }

        if (context.ElementName == "modifierExtension")
        {
            context.Session.Statistics.ModifierExtensionsRemoved++;
        }

        return ValueTask.FromResult(ElementAction.Remove);
    }
}
