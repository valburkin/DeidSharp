using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for references. It passes the rest on.

/// <summary>
/// Rewrites a reference to point at the target's pseudonym and drops its display text
/// (often a name, "Dr. Jane Smith") and logical identifier. A reference that is only a
/// display ("Medicare", with nothing to point at) keeps the display after free-text
/// de-identification, since there is nothing else to keep. A reference that can be
/// neither rewritten nor cleaned is removed.
/// </summary>
public sealed class ReferenceRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.Value is ResourceReference reference ? Rewrite(context, reference) : ElementAction.Pass);
    }

    private static ElementAction Rewrite(FhirElementContext context, ResourceReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Reference))
        {
            if (string.IsNullOrWhiteSpace(reference.Display))
            {
                return ElementAction.Remove;
            }

            // The copy takes the reference's place now; its display is written once the
            // queued texts are done.
            var copy = new ResourceReference { Type = reference.Type };
            context.Session.QueueText(reference.Display, context.Resource, display => copy.Display = display);
            return ElementAction.ReplaceWith(copy);
        }

        var rewritten = context.Session.References.Rewrite(reference.Reference);
        if (rewritten is null)
        {
            return ElementAction.Remove;
        }

        context.Session.Statistics.IdsPseudonymized++;
        return ElementAction.ReplaceWith(new ResourceReference { Reference = rewritten, Type = reference.Type });
    }
}
