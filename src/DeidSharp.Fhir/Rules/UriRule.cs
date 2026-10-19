using System.Collections.Frozen;
using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for URI-valued elements. It passes the rest on.

/// <summary>
/// URI-valued elements. A URI that defines rather than describes is kept: an
/// extension's url, profiles, implicit rules, what a request instantiates, and the
/// canonical url of a definitional resource (a Questionnaire, a ValueSet). Any other
/// URI (a device's url, an endpoint's address) can point at a host, an IP address or a
/// path naming the patient, and is removed. Codings, identifiers and quantities never
/// get here: their own rules handle their systems.
/// </summary>
public sealed class UriRule : IFhirElementRule
{
    private static readonly FrozenSet<string> DefiningElements = FrozenSet.Create(
        StringComparer.Ordinal,
        "profile", "implicitRules", "instantiatesUri", "instantiatesCanonical", "valueSet", "definition", "derivedFrom");

    private static readonly FrozenSet<string> DefinitionalResources = FrozenSet.Create(
        StringComparer.Ordinal,
        "ActivityDefinition", "CapabilityStatement", "ChargeItemDefinition", "CodeSystem", "CompartmentDefinition",
        "ConceptMap", "EffectEvidenceSynthesis", "EventDefinition", "Evidence", "EvidenceVariable", "ExampleScenario",
        "GraphDefinition", "ImplementationGuide", "Library", "Measure", "MessageDefinition", "ObservationDefinition",
        "OperationDefinition", "PlanDefinition", "Questionnaire", "ResearchDefinition", "ResearchElementDefinition",
        "RiskEvidenceSynthesis", "SearchParameter", "SpecimenDefinition", "StructureDefinition", "StructureMap",
        "TerminologyCapabilities", "TestScript", "ValueSet");

    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.Value switch
        {
            // A canonical is its own type, not a FhirUri: it names a definition and is kept.
            not (FhirUri or FhirUrl or Oid or Uuid) => ElementAction.Pass,
            _ when IsDefining(context.Parent, context.ElementName) => ElementAction.Keep,
            _ => ElementAction.Remove,
        });
    }

    /// <summary>True for a URI element that defines rather than describes, and so is kept.</summary>
    public static bool IsDefining(Base parent, string elementName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return DefiningElements.Contains(elementName)
            || (elementName == "url" && parent is Extension)
            || (elementName == "url" && parent is Resource resource && DefinitionalResources.Contains(resource.TypeName));
    }
}
