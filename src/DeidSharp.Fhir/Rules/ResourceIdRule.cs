using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for resource ids and the bundle URLs that name resources.

/// <summary>
/// Resource ids and the bundle URLs that name resources. A resource id becomes its
/// pseudonym (the same one every reference to it gets); <c>Bundle.entry.fullUrl</c> and
/// <c>Bundle.entry.request.url</c> are rewritten the same way. Contained resources keep
/// their local ids, which "#id" references depend on and which identify nothing.
/// Element ids on data types ("id" on a name or an extension) are local too, and kept.
/// </summary>
public sealed class ResourceIdRule : IFhirElementRule
{
    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context switch
        {
            { ElementName: "id", Parent: Resource, Value: Id id } => ResourceId(context, id),
            { ElementName: "id" } => ElementAction.Keep,
            { ElementName: "fullUrl", Parent: Bundle.EntryComponent, Value: FhirUri uri } => Url(context, uri.Value, v => new FhirUri(v)),
            { ElementName: "url", Parent: Bundle.RequestComponent, Value: FhirUri uri } => Url(context, uri.Value, v => new FhirUri(v)),
            _ => ElementAction.Pass,
        });
    }

    private static ElementAction ResourceId(FhirElementContext context, Id id)
    {
        if (context.IsContainedResource || string.IsNullOrWhiteSpace(id.Value))
        {
            return ElementAction.Keep;
        }

        context.Session.Statistics.IdsPseudonymized++;
        return ElementAction.ReplaceWith(new Id(context.Session.References.PseudonymizeId(id.Value)));
    }

    private static ElementAction Url(FhirElementContext context, string? value, Func<string, Base> create)
    {
        var rewritten = context.Session.References.Rewrite(value);
        if (rewritten is null)
        {
            return ElementAction.Remove;
        }

        context.Session.Statistics.IdsPseudonymized++;
        return ElementAction.ReplaceWith(create(rewritten));
    }
}
