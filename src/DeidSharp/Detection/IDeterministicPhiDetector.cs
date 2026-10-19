namespace DeidSharp.Detection;

/// <summary>
/// A detector that runs in-process and synchronously, and gives the same answer every
/// time: no model, no network. The leak guard is built only from these, so the check
/// that runs after a model never shares the model's failure modes.
/// </summary>
public interface IDeterministicPhiDetector : IPhiDetector
{
    /// <param name="cancellationToken">Checked between the searches of a text, so a canceled request stops early.</param>
    IReadOnlyList<PhiSpan> Detect(DetectionRequest request, CancellationToken cancellationToken = default);
}
