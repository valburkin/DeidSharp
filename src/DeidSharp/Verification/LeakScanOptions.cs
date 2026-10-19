using System.Collections.Frozen;

namespace DeidSharp.Verification;

// Design pattern: Parameter Object.
// The pattern: settings that travel together are grouped into one object, instead of
// a long list of separate arguments.
// Here: what a scan reports. An immutable record with defaults, so a caller changes
// only what it needs ("Default with { MinimumScore = 0.5 }").

/// <summary>What <see cref="LeakScanner"/> reports.</summary>
public sealed record LeakScanOptions
{
    public static LeakScanOptions Default { get; } = new();

    /// <summary>Categories not to report, e.g. <see cref="PhiCategory.Url"/> in a field that holds code-system URLs.</summary>
    public IReadOnlySet<PhiCategory> IgnoredCategories { get; init; } = FrozenSet<PhiCategory>.Empty;

    /// <summary>Findings scoring below this are not reported. Default 0.</summary>
    public double MinimumScore { get; init; }

    /// <summary>Also scan JSON property names (a model can leak a name as a key). Default false.</summary>
    public bool ScanPropertyNames { get; init; }

    /// <summary>
    /// The JSON property names that may appear in finding paths (your schema's names).
    /// Any other name is written as "&lt;property N&gt;": paths end up in logs, and a key
    /// written by a model can itself be PHI ({"Ngozi Okafor": …}). Null writes every name
    /// as it is: only for JSON whose keys you control.
    /// </summary>
    public IReadOnlySet<string>? PathPropertyNames { get; init; }
}
