using System.Text;
using DeidSharp.Detection;
using DeidSharp.Diagnostics;
using DeidSharp.Detection.Known;
using DeidSharp.Verification;

namespace DeidSharp.Text;

// Design pattern: Facade.
// The pattern: one simple entry point in front of several cooperating parts, so
// callers don't have to wire them together themselves.
// Here: de-identifying a note means detecting (any stack of detectors), merging spans,
// replacing them (tags or surrogates) and checking the output for leftovers. Callers
// make one call.

/// <summary>
/// De-identifies free text: runs the detector, replaces every span with the
/// replacement strategy, then (by default) verifies that none of the subject's known
/// identifiers survived.
/// </summary>
public sealed class TextDeidentifier
{
    // Only known identifiers are checked: pattern detection would flag the surrogates
    // this class writes itself ("(555) 555-0123" looks like a phone number).
    private static readonly LeakScanner Verifier = new([new KnownIdentifierDetector()]);

    private readonly IPhiDetector _detector;
    private readonly IReplacementStrategy _replacement;
    private readonly TextDeidentifierOptions _options;

    public TextDeidentifier(IPhiDetector detector, IReplacementStrategy replacement, TextDeidentifierOptions? options = null)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
        _options = options ?? TextDeidentifierOptions.Default;
        _options.Validate();
    }

    /// <exception cref="PhiDetectionException">A detector failed.</exception>
    /// <exception cref="ResidualPhiException">A known identifier is still in the output.</exception>
    public async Task<TextDeidentificationResult> DeidentifyAsync(
        string text, TextContext? context = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        context ??= TextContext.None;
        if (text.Length == 0)
        {
            return new TextDeidentificationResult(text, []);
        }

        var detected = await DetectorGuard.DetectAsync(_detector, new DetectionRequest(text, context.Known), cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var spans = SpanMerger.Merge(text, detected).Where(s => s.Score >= _options.MinimumScore);
        var result = Replace(text, spans, context);
        Verify(result.Text, context, cancellationToken);

        // Counted once the output has passed its check: a refused one replaced nothing.
        foreach (var finding in result.Findings)
        {
            DeidSharpTelemetry.RecordReplacement(finding.Category);
        }

        return result;
    }

    // Spans are merged: ordered and disjoint, so the text between them is copied as is.
    private TextDeidentificationResult Replace(string text, IEnumerable<PhiSpan> spans, TextContext context)
    {
        var output = new StringBuilder(text.Length);
        var findings = new List<TextFinding>();
        var position = 0;
        foreach (var span in spans)
        {
            output.Append(text, position, span.Start - position);
            var outputStart = output.Length;
            output.Append(_replacement.Replace(span, text[span.Start..span.End], context));
            findings.Add(new TextFinding(span.Start, span.End, outputStart, output.Length, span.Category, span.Score, span.Sources));
            position = span.End;
        }

        output.Append(text, position, text.Length - position);
        return new TextDeidentificationResult(output.ToString(), findings);
    }

    private void Verify(string output, TextContext context, CancellationToken cancellationToken)
    {
        if (!_options.VerifyOutput || context.Known.Count == 0)
        {
            return;
        }

        var report = Verifier.Scan(output, context.Known, cancellationToken: cancellationToken);
        if (report.IsClean)
        {
            return;
        }

        // Free text of a FHIR resource is refused here too, as "text": it never reaches the
        // resource's own check.
        DeidSharpTelemetry.RecordResidualRefusal("text");
        throw new ResidualPhiException(report);
    }
}
