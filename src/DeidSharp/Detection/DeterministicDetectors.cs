using DeidSharp.Detection.Known;
using DeidSharp.Detection.Patterns;

namespace DeidSharp.Detection;

// Design pattern: Static Factory Method.
// The pattern: a static method that builds a ready-to-use object, so callers don't need
// to know which parts it is made of or how they are set up.
// Here: "the deterministic stack" (the record's known identifiers plus the pattern
// library) is both the default free-text detector and the default leak guard. Building
// it in one place keeps the two from drifting apart.

/// <summary>The detectors that need no model and no network, with their default settings.</summary>
public static class DeterministicDetectors
{
    /// <summary>Known-identifier detection and the pattern library, each a new instance.</summary>
    public static IReadOnlyList<IDeterministicPhiDetector> Create() => [new KnownIdentifierDetector(), new PatternPhiDetector()];
}
