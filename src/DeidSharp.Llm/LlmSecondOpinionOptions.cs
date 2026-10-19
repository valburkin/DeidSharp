using Microsoft.Extensions.AI;

namespace DeidSharp.Llm;

/// <summary>Tuning for <see cref="LlmSecondOpinion"/>.</summary>
public sealed record LlmSecondOpinionOptions
{
    public static LlmSecondOpinionOptions Default { get; } = new();

    /// <summary>
    /// Which spans the model may drop. Default: a span no known-identifier, pattern or LLM detector
    /// reported, since known identifiers are certain, patterns precise, and an LLM's own finding is
    /// already its judgment; and never a location or organization (where NER models put hospital
    /// names), which the model wrongly rejected (docs/evaluation/findings.md, F25 and F26).
    /// </summary>
    public Func<PhiSpan, bool> MayVeto { get; init; } = span =>
        span.Category is not (PhiCategory.Location or PhiCategory.Organization)
        && !span.Sources.Any(s => s == "known" || s.StartsWith("pattern", StringComparison.Ordinal) || s.StartsWith("llm", StringComparison.Ordinal));

    /// <summary>How many candidates one call shows the model (1–200). Default 40.</summary>
    public int MaxCandidatesPerCall { get; init; } = 40;

    /// <summary>Sets what a provider needs beyond these settings, as <see cref="LlmDetectorOptions.ConfigureRequest"/> does.</summary>
    public Action<ChatOptions>? ConfigureRequest { get; init; }

    /// <exception cref="ArgumentException">A value is out of range.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(MayVeto);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxCandidatesPerCall, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxCandidatesPerCall, 200);
    }
}
