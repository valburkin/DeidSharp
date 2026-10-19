namespace DeidSharp.Fhir.Rules;

/// <summary>The built-in rules, in the order they are asked. Earlier rules win.</summary>
public static class DefaultFhirRules
{
    /// <summary>
    /// Removals first (so nothing inside a removed element is processed), then the
    /// type-specific rewrites, then dates, then terminology that must be left alone, and
    /// free text last, as the catch-all for strings.
    /// </summary>
    public static IReadOnlyList<IFhirElementRule> All { get; } =
    [
        new RemovedElementsRule(),
        new RemovedTypesRule(),
        new ExtensionRule(),
        new ResourceIdRule(),
        new ReferenceRule(),
        new IdentifierRule(),
        new UriRule(),
        new AddressRule(),
        new AttachmentRule(),
        new AgeRule(),
        new BirthDateRule(),
        new DateRule(),
        new CodedValueRule(),
        new FreeTextRule(),
    ];
}
