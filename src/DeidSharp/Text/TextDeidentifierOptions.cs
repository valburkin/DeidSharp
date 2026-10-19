using DeidSharp.Verification;

namespace DeidSharp.Text;

// Design pattern: Parameter Object.
// The pattern: settings that travel together are grouped into one object, instead of
// a long list of separate arguments.
// Here: the text de-identifier's tuning. An immutable record with defaults, so a caller
// changes only what it needs ("Default with { VerifyOutput = false }") and the object
// checks its own values.

/// <summary>Tuning for <see cref="TextDeidentifier"/>.</summary>
public sealed record TextDeidentifierOptions
{
    public static TextDeidentifierOptions Default { get; } = new();

    /// <summary>
    /// Spans scoring below this are left in the text. Default 0: every span any detector
    /// reports is replaced (recall first). Raise it only with evaluation results that
    /// justify the lost recall.
    /// </summary>
    public double MinimumScore { get; init; }

    /// <summary>
    /// After replacing, searches the output for the subject's known identifiers and throws
    /// <see cref="ResidualPhiException"/> if any remain. Default true.
    /// </summary>
    public bool VerifyOutput { get; init; } = true;

    /// <exception cref="ArgumentOutOfRangeException"><see cref="MinimumScore"/> is not between 0 and 1.</exception>
    internal void Validate()
    {
        if (double.IsNaN(MinimumScore) || MinimumScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumScore), MinimumScore, "MinimumScore must be between 0 and 1.");
        }
    }
}
