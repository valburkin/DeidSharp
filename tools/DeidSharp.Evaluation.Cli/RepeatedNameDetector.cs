using DeidSharp.Detection;
using DeidSharp.Detection.Known;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Decorator.
// The pattern: a wrapper with the same interface as the object it wraps, adding behavior
// before or after passing each call through, so callers can't tell the two apart.
// Here: it is an IPhiDetector around a detector stack. After the stack has run, it looks
// for every other mention of the names the stack found, the way a human reviewer would.

/// <summary>
/// Finds the other mentions of the names a detector stack found in the same text: a note
/// that names "Dr. Ellen Hart" once and "Hart" three times more needs all four replaced,
/// and a model can miss a bare surname with little context. The names found are searched for
/// as known names (<see cref="KnownIdentifierDetector"/>): by any single part, ignoring case
/// and accents, whole words only, and an ambiguous part such as "May" only after a title.
/// </summary>
/// <remarks>
/// An experiment, kept here so <c>--variants repeated-names</c> reproduces F7: it added almost
/// nothing on either corpus, so the libraries don't ship it. It adds spans and never removes
/// one (recall first), so it can only add false positives where the stack already named
/// something wrongly.
/// </remarks>
internal sealed class RepeatedNameDetector : IPhiDetector
{
    /// <summary>The source of the spans this detector adds.</summary>
    public const string Source = "repeated-name";

    // Each part runs inside a composite of one, which holds it to the detector contract:
    // a failure or a null answer stops the run, and every span lies inside the text.
    private readonly IPhiDetector _inner;
    private readonly CompositePhiDetector _guarded;
    private readonly CompositePhiDetector _names;

    public RepeatedNameDetector(IPhiDetector inner, KnownIdentifierOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _guarded = new CompositePhiDetector([inner]);
        _names = new CompositePhiDetector([new KnownIdentifierDetector(options)]);
    }

    public string Name => _inner.Name;

    public async Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spans = await _guarded.DetectAsync(request, cancellationToken);
        var found = spans.Where(s => s.Category == PhiCategory.Name).Aggregate(
            KnownIdentifiers.Empty, (known, s) => known.AddName(request.Text[s.Start..s.End]));
        if (found.Count == 0)
        {
            return spans;
        }

        var mentions = await _names.DetectAsync(new DetectionRequest(request.Text, found), cancellationToken);
        return SpanMerger.Merge(
            request.Text,
            spans.Concat(mentions.Select(m => new PhiSpan(m.Start, m.End, PhiCategory.Name, m.Score, Source))));
    }
}
