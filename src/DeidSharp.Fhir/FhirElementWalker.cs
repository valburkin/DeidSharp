using System.Collections;
using System.Globalization;
using Hl7.Fhir.Model;
using DeidSharp.Fhir.Rules;

namespace DeidSharp.Fhir;

/// <summary>
/// Visits every element of a resource (and of the resources inside it: bundle entries,
/// contained resources) and offers it to the rules, applying the first decision. An
/// element no rule claims is descended into if it is complex and kept if it is a
/// primitive the rules left alone (a code, a number). A kept primitive's extensions are
/// still offered to the rules. A complex element left empty after its children were
/// processed is removed.
///
/// Not the GoF Visitor pattern, despite walking a tree: a Visitor adds operations per
/// node type through double dispatch. This is a plain traversal that asks the rule chain
/// what to do with each element.
/// </summary>
internal sealed class FhirElementWalker(IReadOnlyList<IFhirElementRule> rules, FhirDeidentificationSession session)
{
    public Task WalkAsync(Resource resource) => WalkChildrenAsync(resource, resource, contained: false, resource.TypeName);

    private async Task WalkChildrenAsync(Base node, Resource resource, bool contained, string path)
    {
        foreach (var (name, value) in node.EnumerateElements().ToList())
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            switch (value)
            {
                case IList list:
                    await WalkListAsync(node, name, list, resource, contained, path).ConfigureAwait(false);
                    break;
                case Base child:
                    var result = await ProcessAsync(node, name, child, resource, contained, $"{path}.{name}").ConfigureAwait(false);
                    if (!ReferenceEquals(result, child))
                    {
                        node.SetValue(name, result);
                    }

                    break;
            }
        }
    }

    // Backwards, so removing an item leaves the positions of the items still to visit unchanged.
    private async Task WalkListAsync(Base node, string name, IList list, Resource resource, bool contained, string path)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is not Base item)
            {
                continue;
            }

            var itemPath = string.Create(CultureInfo.InvariantCulture, $"{path}.{name}[{i}]");
            var result = await ProcessAsync(node, name, item, resource, contained, itemPath).ConfigureAwait(false);
            if (result is null)
            {
                list.RemoveAt(i);
            }
            else if (!ReferenceEquals(result, item))
            {
                list[i] = result;
            }
        }

        if (list.Count == 0)
        {
            node.SetValue(name, null);
        }
    }

    // Returns what belongs in the element's place: the element itself, its replacement,
    // or null when it is removed.
    private async Task<Base?> ProcessAsync(Base parent, string name, Base value, Resource resource, bool contained, string path)
    {
        if (value is Resource inner)
        {
            // A resource inside this one (a bundle entry, a contained resource): its own
            // scope, so dates and text in it belong to its own patient.
            await WalkChildrenAsync(inner, inner, contained: name == "contained", path).ConfigureAwait(false);
            return value;
        }

        var action = await DecideAsync(new FhirElementContext(parent, name, value, resource, contained, path, session)).ConfigureAwait(false);
        switch (action.Kind)
        {
            case ElementActionKind.Descend:
                await WalkChildrenAsync(value, resource, contained, path).ConfigureAwait(false);

                // Everything inside was removed: an empty element is invalid FHIR and
                // says nothing, so it goes too.
                return value.EnumerateElements().Any() ? value : Removed();
            case ElementActionKind.Remove:
                return Removed();
            case ElementActionKind.Replace:
                session.Statistics.ElementsReplaced++;
                return action.Replacement;
            default:
                // Kept as it is. A primitive can still carry extensions ("_gender":
                // {"extension": …}): those go through the rules like any other extension.
                if (value is PrimitiveType { Extension.Count: > 0 })
                {
                    await WalkChildrenAsync(value, resource, contained, path).ConfigureAwait(false);
                }

                return value;
        }
    }

    // The first rule that does not pass decides. When every rule passes, a complex
    // element is descended into and a primitive is kept.
    private async ValueTask<ElementAction> DecideAsync(FhirElementContext context)
    {
        foreach (var rule in rules)
        {
            var action = await rule.ApplyAsync(context).ConfigureAwait(false);
            if (action.Kind != ElementActionKind.Pass)
            {
                return action;
            }
        }

        return context.Value is PrimitiveType ? ElementAction.Keep : ElementAction.Descend;
    }

    private Base? Removed()
    {
        session.Statistics.ElementsRemoved++;
        return null;
    }
}
