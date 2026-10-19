using System.Collections;
using System.Globalization;
using Hl7.Fhir.Model;

namespace DeidSharp.Fhir;

/// <summary>
/// The child elements of a FHIR element: each single child, and each item of a
/// repeating one, with its name and position. A primitive's own value (the text of a
/// string, the bytes of base64) is not an element and is not returned.
/// </summary>
internal static class FhirChildren
{
    public static IEnumerable<FhirChild> Of(Base node)
    {
        foreach (var (name, value) in node.EnumerateElements())
        {
            switch (value)
            {
                case Base child:
                    yield return new FhirChild(name, null, child);
                    break;
                case IList list:
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is Base item)
                        {
                            yield return new FhirChild(name, i, item);
                        }
                    }

                    break;
            }
        }
    }
}

/// <summary>One child element: its name in the parent, its position if the element repeats, and the element.</summary>
internal readonly record struct FhirChild(string Name, int? Index, Base Value)
{
    /// <summary>The child's path below its parent's, e.g. "Patient.name[0]" below "Patient".</summary>
    public string PathFrom(string parentPath) => Index is { } index
        ? string.Create(CultureInfo.InvariantCulture, $"{parentPath}.{Name}[{index}]")
        : $"{parentPath}.{Name}";
}
