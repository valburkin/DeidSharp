using System.Text.Json;
using DeidSharp.Detection;

namespace DeidSharp.Verification;

/// <summary>
/// The last line of defense: scans output that is supposed to be de-identified (a
/// model's summary, a de-identified resource) and reports anything that still looks
/// like PHI. Built only from deterministic detectors, so it never shares the failure
/// modes of the model or service whose output it checks.
/// </summary>
public sealed class LeakScanner
{
    private readonly IReadOnlyList<IDeterministicPhiDetector> _detectors;

    public LeakScanner(IEnumerable<IDeterministicPhiDetector> detectors)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        _detectors = [.. detectors];

        // Callers that don't use nullable annotations can still pass a null.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_detectors.Count == 0 || _detectors.Any(d => d is null))
        {
            throw new ArgumentException("A leak scanner needs at least one detector and no nulls.", nameof(detectors));
        }
    }

    /// <summary>The deterministic detectors (<see cref="DeterministicDetectors"/>) with their default settings.</summary>
    public static LeakScanner CreateDefault() => new(DeterministicDetectors.Create());

    /// <param name="cancellationToken">Checked between values and between the searches of a value.</param>
    public LeakReport Scan(
        string text, KnownIdentifiers? known = null, LeakScanOptions? options = null, CancellationToken cancellationToken = default) =>
        Scan([new ScannedValue("$", text)], known, options, cancellationToken);

    /// <param name="cancellationToken">Checked between values and between the searches of a value.</param>
    public LeakReport Scan(
        IEnumerable<ScannedValue> values, KnownIdentifiers? known = null, LeakScanOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        options ??= LeakScanOptions.Default;
        var findings = new List<LeakFinding>();
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Text.Length == 0)
            {
                continue;
            }

            var request = new DetectionRequest(value.Text, known);
            var spans = _detectors.SelectMany(d => d.Detect(request, cancellationToken))
                .Where(s => s.Score >= options.MinimumScore && !options.IgnoredCategories.Contains(s.Category));
            findings.AddRange(SpanMerger.Merge(value.Text, spans)
                .Select(s => new LeakFinding(value.Path, s.Start, s.End, s.Category, s.Sources)));
        }

        return new LeakReport(findings);
    }

    /// <summary>
    /// Scans every string value in a JSON document (and, if
    /// <see cref="LeakScanOptions.ScanPropertyNames"/> is set, every property name).
    /// Paths are written like "$.conditions[0].name"; set
    /// <see cref="LeakScanOptions.PathPropertyNames"/> when the keys come from a model.
    /// </summary>
    public LeakReport ScanJson(
        JsonElement root, KnownIdentifiers? known = null, LeakScanOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= LeakScanOptions.Default;
        return Scan(JsonStringCollector.Collect(root, options), known, options, cancellationToken);
    }
}

/// <summary>One value to scan, and where it came from (a JSON path, a field name).</summary>
public sealed record ScannedValue(string Path, string Text)
{
    public string Text { get; } = Text ?? throw new ArgumentNullException(nameof(Text));
}
