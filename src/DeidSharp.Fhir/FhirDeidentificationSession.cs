using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir.References;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Text;

namespace DeidSharp.Fhir;

// Design pattern: Context Object.
// The pattern: the services and state that many steps of one operation share travel
// together in one object, instead of as a long list of arguments to every step.
// Here: every rule receives the session (through FhirElementContext) and takes what it
// needs: the mode, the pseudonymizer, the date strategy, the reference rewriter, the
// queue for free text and the run's statistics. A new rule needs no new parameters.

/// <summary>
/// The services and state of one de-identification run, shared by every rule: the mode,
/// the pseudonymizer and date strategy, the reference rewriter, the record's known
/// identifiers, free-text de-identification and the run's statistics.
/// </summary>
public sealed class FhirDeidentificationSession
{
    private readonly SubjectResolver _subjects;
    private readonly TextDeidentifier _text;
    private readonly FreeTextQueue _queue;

    internal FhirDeidentificationSession(
        FhirDeidentificationOptions options,
        IPseudonymizer pseudonymizer,
        IDateTransformer dates,
        ReferenceRewriter references,
        TextDeidentifier text,
        KnownIdentifiers known,
        SubjectResolver subjects,
        DateOnly referenceDate,
        CancellationToken cancellationToken)
    {
        Options = options;
        Pseudonymizer = pseudonymizer;
        Dates = dates;
        References = references;
        Known = known;
        ReferenceDate = referenceDate;
        CancellationToken = cancellationToken;
        _text = text;
        _queue = new FreeTextQueue(text, options.MaxFreeTextConcurrency);
        _subjects = subjects;
    }

    public FhirDeidentificationOptions Options { get; }

    public FhirDeidentificationMode Mode => Options.Mode;

    public IPseudonymizer Pseudonymizer { get; }

    public IDateTransformer Dates { get; }

    public ReferenceRewriter References { get; }

    /// <summary>Every identifier found in the input's structured fields (patients, relatives, staff, sites).</summary>
    public KnownIdentifiers Known { get; }

    /// <summary>The date ages are computed at.</summary>
    public DateOnly ReferenceDate { get; }

    public CancellationToken CancellationToken { get; }

    public FhirDeidentificationStatistics Statistics { get; } = new();

    /// <summary>The patient a resource belongs to (see <see cref="SubjectResolver"/>).</summary>
    public string SubjectOf(Resource resource) => _subjects.SubjectOf(resource);

    /// <summary>
    /// De-identifies free text that belongs to <paramref name="resource"/>'s patient, now.
    /// The built-in rules use <see cref="QueueText"/> instead, so the texts of a bundle do
    /// not wait on each other.
    /// </summary>
    public async Task<string> DeidentifyTextAsync(string text, Resource resource)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resource);
        Statistics.FreeTextValues++;
        var result = await _text.DeidentifyAsync(text, new TextContext(SubjectOf(resource), Known), CancellationToken)
            .ConfigureAwait(false);
        Statistics.FreeTextFindings += result.Findings.Count;
        return result.Text;
    }

    /// <summary>
    /// Queues free text that belongs to <paramref name="resource"/>'s patient and returns at
    /// once. When the walk is done, the run de-identifies every queued text, up to
    /// <see cref="FhirDeidentificationOptions.MaxFreeTextConcurrency"/> at a time, and then
    /// hands each result to its <paramref name="apply"/>, one after another in the order
    /// queued. The output is verified only after that, and if any text fails, the run fails.
    /// Until then the element still holds the original text.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called from a write-back, once the queued texts are being de-identified.</exception>
    public void QueueText(string text, Resource resource, Action<string> apply)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(apply);
        _queue.Add(text, new TextContext(SubjectOf(resource), Known), apply);
        Statistics.FreeTextValues++;
    }

    /// <summary>De-identifies the queued texts and writes their results back (see <see cref="QueueText"/>).</summary>
    internal async Task DeidentifyQueuedTextAsync()
    {
        if (_queue.Count > 0)
        {
            Statistics.FreeTextFindings += await _queue.DeidentifyAsync(CancellationToken).ConfigureAwait(false);
        }
    }
}
