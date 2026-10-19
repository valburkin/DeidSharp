namespace DeidSharp.Fhir;

/// <summary>Counts from one run, for logs and audit (no values).</summary>
public sealed class FhirDeidentificationStatistics
{
    public int ElementsRemoved { get; internal set; }

    public int ElementsReplaced { get; internal set; }

    public int IdsPseudonymized { get; internal set; }

    public int FreeTextValues { get; internal set; }

    public int FreeTextFindings { get; internal set; }

    /// <summary>
    /// Modifier extensions removed because they were not allowed. A modifier extension
    /// can change a resource's meaning; review any non-zero count.
    /// </summary>
    public int ModifierExtensionsRemoved { get; internal set; }
}
