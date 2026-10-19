namespace DeidSharp.Detection;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: each way of finding PHI in text (regex patterns, the record's own known
// identifiers, a Presidio sidecar, a future LLM) is one detector. The de-identifier
// runs whatever detectors it is handed, so a provider is swapped or added without
// touching the de-identifier.

/// <summary>
/// Finds PHI in a piece of text. One detector serves many texts at once (the notes of a
/// bundle, the requests of a service), so implementations must be safe to call concurrently.
/// </summary>
public interface IPhiDetector
{
    /// <summary>A short, stable name recorded as the source of every span this detector returns.</summary>
    string Name { get; }

    /// <summary>
    /// Returns the spans this detector believes are PHI. Spans may overlap; the caller
    /// merges them. A detector that cannot run (a sidecar is down) must throw, never
    /// return an empty list: an empty list means "no PHI found".
    /// </summary>
    Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default);
}
