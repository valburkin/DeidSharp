namespace DeidSharp.Detection.Known;

/// <summary>
/// One known identifier's search, built by <see cref="KnownValuePatterns"/>: the regular
/// expression (over folded text, ignoring case) and its anchors, literals of which every
/// match contains at least one, looked for in <see cref="View"/>. A search without anchors
/// always runs.
/// </summary>
internal sealed record KnownValuePattern(string Pattern, AnchorView View, IReadOnlyList<string> Anchors);
