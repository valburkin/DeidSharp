using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli.Tests.TestDoubles;

// Design pattern: Test Double (Stub).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
// Here: a detector that reports every occurrence of the words a test chose, so scoring
// can be checked against known hits and misses without real detection.

/// <summary>Reports each occurrence of its words as a span of their category.</summary>
internal sealed class ScriptedDetector(params (string Word, PhiCategory Category)[] words) : IPhiDetector
{
    public string Name => "scripted";

    public Task<IReadOnlyList<PhiSpan>> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
    {
        var spans = new List<PhiSpan>();
        foreach (var (word, category) in words)
        {
            for (var at = request.Text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = request.Text.IndexOf(word, at + word.Length, StringComparison.Ordinal))
            {
                spans.Add(new PhiSpan(at, at + word.Length, category, 1.0, Name));
            }
        }

        return Task.FromResult<IReadOnlyList<PhiSpan>>(spans);
    }
}
