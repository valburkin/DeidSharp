using Hl7.Fhir.Model;

namespace DeidSharp.Fhir;

/// <summary>
/// Copies an element without any extension, modifier extension or element id, at any
/// depth (including the extensions a primitive inside it can carry). Rules that keep an
/// element whole without inspecting it (a coding, a quantity) keep this copy, so nothing
/// can ride along unchecked in an extension.
/// </summary>
internal static class CleanCopy
{
    public static T Of<T>(T element)
        where T : Base
    {
        var copy = element.DeepCopy();
        Strip(copy);
        return copy;
    }

    private static void Strip(Base node)
    {
        var removed = node.EnumerateElements()
            .Select(element => element.Key)
            .Where(name => name is "extension" or "modifierExtension" || (name == "id" && node is not Resource))
            .ToList();
        foreach (var name in removed)
        {
            node.SetValue(name, null);
        }

        foreach (var child in FhirChildren.Of(node))
        {
            Strip(child.Value);
        }
    }
}
