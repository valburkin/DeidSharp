namespace DeidSharp.Detection.Patterns;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one IPhiDetector, the one that finds identifiers by their shape. It stacks with
// the known-identifier and NER detectors, and is part of the leak guard. Not a design
// pattern detector, despite the name: "pattern" here means a regular expression.

/// <summary>
/// Finds PHI that has a recognizable shape (SSNs, phone numbers, emails, dates, labeled
/// record numbers, addresses, ages over 89…) with the rules in <see cref="PatternLibrary"/>.
/// High precision on formatted identifiers; it cannot find names in prose except after
/// a title or a label ("Dr. Smith", "daughter Mary"), which is what NER layers are for.
/// </summary>
public sealed class PatternPhiDetector : IDeterministicPhiDetector
{
    /// <param name="rules">The rules to run; null runs <see cref="PatternLibrary.Default"/>.</param>
    public PatternPhiDetector(IEnumerable<PatternRule>? rules = null)
    {
        Rules = [.. rules ?? PatternLibrary.Default];
        UniqueNames.Ensure(Rules.Select(r => r.Name), "Pattern rule", nameof(rules));
    }

    public string Name => "pattern";

    public IReadOnlyList<PatternRule> Rules { get; }

    public IReadOnlyList<PhiSpan> Detect(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spans = new List<PhiSpan>();
        foreach (var rule in Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spans.AddRange(rule.Pattern.Matches(request.Text)
                .Where(match => rule.Validator?.Invoke(match) ?? true)
                .Select(match => rule.ValueGroup is null ? match : match.Groups[rule.ValueGroup])
                .Where(value => value is { Success: true, Length: > 0 })
                .Select(value => new PhiSpan(value.Index, value.Index + value.Length, rule.Category, rule.Score, $"{Name}:{rule.Name}")));
        }

        return spans;
    }

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detect(request, cancellationToken));
}
