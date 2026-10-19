using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for attachments and for document content. It passes the rest on.

/// <summary>
/// Attachments. Inline UTF-8 text (<c>text/*</c>, as in clinical notes) is decoded, run
/// through free-text de-identification and re-encoded; its hash is dropped (it would
/// fingerprint the original), and so is its size, which the FHIR SDK writes in a form
/// R4 parsers reject (it is optional). Anything else (images, PDFs, attachments by URL,
/// text in another charset or holding control characters) cannot be inspected and is
/// removed.
/// </summary>
public sealed class AttachmentRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A document's content is nothing without its attachment (FHIR requires one), so
        // content whose attachment cannot be kept goes with it.
        if (context.Value is DocumentReference.ContentComponent content)
        {
            return ValueTask.FromResult(
                content.Attachment is { } inner && InlineText.IsReadable(inner) ? ElementAction.Pass : ElementAction.Remove);
        }

        return ValueTask.FromResult(context.Value is Attachment attachment ? Clean(context, attachment) : ElementAction.Pass);
    }

    // The copy takes the attachment's place now; its data is written once the queued
    // texts are done.
    private static ElementAction Clean(FhirElementContext context, Attachment attachment)
    {
        if (!InlineText.TryDecode(attachment, out var text))
        {
            return ElementAction.Remove;
        }

        var copy = new Attachment { ContentType = attachment.ContentType, Language = attachment.Language };
        context.Session.QueueText(text, context.Resource, cleaned => copy.Data = InlineText.Encode(cleaned));
        return ElementAction.ReplaceWith(copy);
    }
}
