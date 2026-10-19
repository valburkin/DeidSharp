namespace DeidSharp.Presidio;

// Design pattern: Options.
// The pattern: a class of settings that the host fills from configuration and hands to
// a service, instead of the service reading configuration itself.
// Here: AddPresidioDetector binds these through IOptions<PresidioOptions>. The setters
// are public because the options system fills them after construction; Validate runs
// before any detector uses them.

/// <summary>Where the Presidio analyzer runs and how its findings are read.</summary>
public sealed class PresidioOptions
{
    /// <summary>The analyzer's base address, e.g. http://localhost:5002/.</summary>
    public Uri Endpoint { get; set; } = new("http://localhost:5002/");

    /// <summary>
    /// The detector's name, recorded as the source of its spans. Give two Presidio
    /// detectors in one stack different names (e.g. "presidio" and "presidio-clinical").
    /// </summary>
    public string Name { get; set; } = "presidio";

    public string Language { get; set; } = "en";

    /// <summary>
    /// Findings scoring below this are dropped by the analyzer. Default 0.3: low on
    /// purpose, because a missed identifier costs more than a redacted word. Presidio
    /// scores unlabeled phone numbers at 0.4, for example.
    /// </summary>
    public double ScoreThreshold { get; set; } = 0.3;

    /// <summary>Only ask for these Presidio entity types; null or empty asks for all.</summary>
    public IList<string>? Entities { get; set; }

    /// <summary>
    /// Presidio entity types to ignore. Default: NRP (nationality, religious or political
    /// group), which is sensitive but not an identifier under Safe Harbor, and CRYPTO.
    /// </summary>
    public ISet<string> IgnoredEntities { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NRP", "CRYPTO" };

    /// <summary>
    /// Records which of the analyzer's recognizers found each span in the span's source
    /// ("clinical:SpacyRecognizer" instead of "clinical"), for telling which part of a stack
    /// a finding came from. Stock Presidio says so only when asked for its decision process,
    /// which costs a little time per request; the DeidSharp clinical sidecar always says so.
    /// Default false: sources stay the detector's name.
    /// </summary>
    public bool ReportRecognizers { get; set; }

    /// <summary>Overrides or additions to <see cref="PresidioEntityMap.Default"/> (entity type → category).</summary>
    public IDictionary<string, PhiCategory> EntityMap { get; set; } = new Dictionary<string, PhiCategory>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How long one detection may wait on the analyzer, including any tries again that the
    /// HTTP pipeline makes. Default 30 seconds (transformer models are slow on CPU).
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (Endpoint is null || !Endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("PresidioOptions.Endpoint must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Language))
        {
            throw new ArgumentException("PresidioOptions.Name and Language are required.");
        }

        if (double.IsNaN(ScoreThreshold) || ScoreThreshold is < 0 or > 1)
        {
            throw new ArgumentException("PresidioOptions.ScoreThreshold must be between 0 and 1.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("PresidioOptions.Timeout must be positive.");
        }
    }
}
