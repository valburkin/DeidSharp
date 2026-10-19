namespace DeidSharp.Fhir;

/// <summary>Which de-identification standard a run follows.</summary>
public enum FhirDeidentificationMode
{
    /// <summary>
    /// HIPAA Safe Harbor: identifiers removed, dates cut to the year, addresses cut to
    /// state and three-digit ZIP, ages over 89 removed. Resource ids become random-keyed
    /// pseudonyms so references inside the output still resolve.
    /// </summary>
    SafeHarbor,

    /// <summary>
    /// Pseudonymization: identifiers and ids become keyed pseudonyms that stay the same
    /// across runs with the same key (so data can be linked over time), and dates are
    /// shifted per patient instead of cut to the year. Not Safe Harbor: use it under an
    /// expert determination or a data use agreement.
    /// </summary>
    Pseudonymized,
}
