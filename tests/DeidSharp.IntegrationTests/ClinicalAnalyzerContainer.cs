namespace DeidSharp.IntegrationTests;

/// <summary>The DeidSharp clinical analyzer built from sidecar/ (Presidio + an i2b2-trained transformer).</summary>
public sealed class ClinicalAnalyzerContainer : AnalyzerContainer
{
    protected override string Image => Environment.GetEnvironmentVariable("DEIDSHARP_CLINICAL_IMAGE") ?? "localhost/deidsharp/clinical-analyzer:local";

    protected override string ExtraSwitch => "DEIDSHARP_CLINICAL";
}
