using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

/// <summary>One element offered to the rules: where it sits and what is around it.</summary>
/// <param name="Parent">The element that holds it.</param>
/// <param name="ElementName">Its name in the parent, e.g. "birthDate" (choice elements without the type suffix: "deceased").</param>
/// <param name="Value">The element itself (one item, for a repeating element).</param>
/// <param name="Resource">The nearest resource it belongs to.</param>
/// <param name="IsContainedResource">Whether <paramref name="Resource"/> is a contained resource.</param>
/// <param name="Path">A readable path for reports, e.g. "Bundle.entry[2].resource.name[0]".</param>
/// <param name="Session">The run's shared services and state.</param>
public sealed record FhirElementContext(
    Base Parent,
    string ElementName,
    Base Value,
    Resource Resource,
    bool IsContainedResource,
    string Path,
    FhirDeidentificationSession Session);
