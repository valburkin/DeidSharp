namespace DeidSharp.IntegrationTests;

/// <summary>Microsoft's stock analyzer: spaCy NER plus pattern recognizers.</summary>
public sealed class StockPresidioContainer : AnalyzerContainer
{
    /// <summary>The image docker-compose.yml pins too (PinnedImageTests keeps them the same).</summary>
    public const string PinnedImage = "mcr.microsoft.com/presidio-analyzer:2.2.362@sha256:286e3fa7f3a7426e775e8564fe1870f1ba8f999d3ab8bbb8cc46a44355d9d6e9";

    protected override string Image => Environment.GetEnvironmentVariable("DEIDSHARP_PRESIDIO_IMAGE") ?? PinnedImage;
}
