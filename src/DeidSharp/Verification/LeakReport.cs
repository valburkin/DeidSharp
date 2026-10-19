using System.Globalization;

namespace DeidSharp.Verification;

// Design pattern: Result object (Notification).
// The pattern: return success or failure, with every problem found, as a value instead
// of throwing on the first problem.
// Here: a scan reports every suspected identifier at once, so a caller can log them all,
// retry, or decide to throw (ResidualPhiException carries the same report).

/// <summary>What a <see cref="LeakScanner"/> found. Holds positions and categories, never the values.</summary>
public sealed record LeakReport(IReadOnlyList<LeakFinding> Findings)
{
    public bool IsClean => Findings.Count == 0;

    /// <summary>One line per finding, safe to log: "$.conditions[0]: possible Ssn at [12, 23) (pattern:ssn)".</summary>
    public IReadOnlyList<string> Describe() =>
    [
        .. Findings.Select(f => string.Create(CultureInfo.InvariantCulture,
            $"{f.Path}: possible {f.Category} at [{f.Start}, {f.End}) ({string.Join("+", f.Sources)})")),
    ];
}

/// <summary>One suspected identifier in scanned output.</summary>
public sealed record LeakFinding(string Path, int Start, int End, PhiCategory Category, IReadOnlyList<string> Sources);
