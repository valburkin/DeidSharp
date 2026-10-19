using System.Text.RegularExpressions;

namespace DeidSharp.Detection.Patterns;

/// <summary>
/// One row of the pattern table (<see cref="PatternLibrary"/>): a regular expression for
/// one kind of PHI, the confidence a match earns, and optionally the named group that
/// holds the PHI itself (so "MRN: 12345" redacts "12345" and keeps the label) and a
/// check the matched value must pass (a checksum, a valid month).
/// </summary>
/// <param name="Name">Stable rule name, e.g. "ssn"; recorded as "pattern:ssn" on each span.</param>
/// <param name="ValueGroup">Named group holding the PHI; null for the whole match.</param>
/// <param name="Validator">Receives the match; returns false to discard it.</param>
public sealed record PatternRule(
    string Name,
    PhiCategory Category,
    Regex Pattern,
    double Score,
    string? ValueGroup = null,
    Func<Match, bool>? Validator = null);
