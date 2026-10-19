using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DeidSharp.Text;

namespace DeidSharp.Detection.Known;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one IPhiDetector, the one that looks for the record's own identifiers. It
// stacks with the pattern library and NER detectors, and is the core of the leak guard.

/// <summary>
/// Finds the record's own identifiers (<see cref="DetectionRequest.Known"/>) in free
/// text: case- and accent-insensitive, whole words only, in every common written form
/// (see <see cref="KnownValuePatterns"/>). Every hit scores 1.0: it is not a guess.
///
/// This is the most reliable layer for a single patient's notes, and the one the leak
/// guard leans on. Its limits: it only finds what the record lists, and identifiers
/// shorter than <see cref="KnownIdentifierOptions.MinimumIdentifierLength"/> are skipped.
/// </summary>
public sealed class KnownIdentifierDetector : IDeterministicPhiDetector
{
    // Design pattern: Memoization (a cache keyed by the input).
    // The pattern: the result of an expensive computation is stored against its input,
    // so the same input never pays for it twice.
    // Here: the searches and anchor index per identifier set and options, shared by every
    // detector: one FHIR run searches the same set for free text, for its own output check
    // and for surrogates, from several threads at once. Sets are immutable, so the searches
    // never go stale; the weak table lets a set and its searches be collected together; and
    // Lazy builds each set's searches once, however many threads ask for them at the same time.
    private static readonly ConditionalWeakTable<KnownIdentifiers, ConcurrentDictionary<KnownIdentifierOptions, Lazy<CompiledSet>>> Cache = [];

    private readonly KnownIdentifierOptions _options;

    public KnownIdentifierDetector(KnownIdentifierOptions? options = null)
    {
        _options = options ?? KnownIdentifierOptions.Default;
        _options.Validate();
    }

    public string Name => "known";

    public IReadOnlyList<PhiSpan> Detect(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Known.Count == 0 || request.Text.Length == 0)
        {
            return [];
        }

        var folded = TextFolding.Fold(request.Text);
        var known = request.Known;
        var set = Cache.GetValue(known, static _ => new ConcurrentDictionary<KnownIdentifierOptions, Lazy<CompiledSet>>())
            .GetOrAdd(_options, static (options, known) => new Lazy<CompiledSet>(() => Compile(known, options)), known)
            .Value;
        var spans = new List<PhiSpan>();
        foreach (var search in set.Index.Candidates(folded).Select(i => set.Searches[i]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            spans.AddRange(search.Pattern.Value.Matches(folded)
                .Where(match => match.Length > 0)
                .Select(match => new PhiSpan(match.Index, match.Index + match.Length, search.Category, 1.0, Name)));
        }

        return spans;
    }

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detect(request, cancellationToken));

    // Design pattern: Lazy Initialization.
    // The pattern: an object is created the first time it is needed, not up front, so
    // what is never used is never paid for.
    // Here: a search's regular expression is built the first time a text holds one of its
    // anchors. In a large bundle most identifiers never appear in most texts.
    private static CompiledSet Compile(KnownIdentifiers known, KnownIdentifierOptions options)
    {
        var built = known.Items
            .Select(item => (item.Category, Pattern: KnownValuePatterns.Build(item, options)))
            .Where(x => x.Pattern is not null)
            .Select(x => (x.Category, Pattern: x.Pattern!))
            .ToList();
        return new CompiledSet(
            [
                .. built.Select(x => new Search(
                    x.Category,
                    new Lazy<Regex>(() => new Regex(x.Pattern.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, options.MatchTimeout)))),
            ],
            new AnchorIndex([.. built.Select(x => x.Pattern)]));
    }

    private sealed record Search(PhiCategory Category, Lazy<Regex> Pattern);

    // The searches of one identifier set, and the index that says which can match a text.
    private sealed record CompiledSet(IReadOnlyList<Search> Searches, AnchorIndex Index);
}
