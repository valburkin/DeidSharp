namespace DeidSharp.Text;

/// <summary>
/// De-identified text and what was replaced in it. Findings record positions and
/// categories only, never the original values, so a result can be logged.
/// </summary>
public sealed record TextDeidentificationResult(string Text, IReadOnlyList<TextFinding> Findings);

/// <summary>One replacement: where it was in the input, where its replacement is in the output.</summary>
public sealed record TextFinding(
    int InputStart,
    int InputEnd,
    int OutputStart,
    int OutputEnd,
    PhiCategory Category,
    double Score,
    IReadOnlyList<string> Sources);
