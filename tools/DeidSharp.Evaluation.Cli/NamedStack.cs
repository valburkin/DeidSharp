using DeidSharp.Detection;
using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli;

/// <summary>
/// A DeidSharp detector stack and the name it is reported under. Detection gets the text
/// only, no known identifiers: the corpora come without the structured record those would
/// come from.
/// </summary>
/// <param name="Baseline">The stack this one is a variant of, if any, for a paired comparison with it.</param>
internal sealed record NamedStack(string Name, IPhiDetector Detector, string? Baseline = null) : IScoredStack
{
    public Task<IReadOnlyList<PhiSpan>> PredictAsync(CorpusDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Detector.DetectAsync(new DetectionRequest(document.Text), cancellationToken);
    }
}
