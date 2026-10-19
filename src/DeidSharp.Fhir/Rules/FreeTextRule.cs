using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the last handler in the default chain, the catch-all for strings no earlier
// rule claimed.

/// <summary>
/// Every remaining string and markdown value (notes, comments, descriptions, a coded
/// concept's free-text label) goes through free-text de-identification. This is the
/// catch-all: a string no earlier rule claimed is treated as text that may hold PHI. The
/// text is queued (<see cref="FhirDeidentificationSession.QueueText"/>) and the element
/// keeps its place; its value is rewritten once the queued texts are done.
/// </summary>
public sealed class FreeTextRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.Value switch
        {
            Markdown markdown => Queue(context, markdown.Value, cleaned => markdown.Value = cleaned),
            FhirString text => Queue(context, text.Value, cleaned => text.Value = cleaned),
            _ => ElementAction.Pass,
        });
    }

    private static ElementAction Queue(FhirElementContext context, string? text, Action<string> rewrite)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ElementAction.Keep;
        }

        context.Session.QueueText(text, context.Resource, cleaned =>
        {
            if (cleaned == text)
            {
                return;
            }

            // The element stays in place, so the walker never counts it: the rule does.
            rewrite(cleaned);
            context.Session.Statistics.ElementsReplaced++;
        });
        return ElementAction.Keep;
    }
}
