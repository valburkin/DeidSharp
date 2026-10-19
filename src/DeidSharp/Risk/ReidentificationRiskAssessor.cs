namespace DeidSharp.Risk;

/// <summary>
/// Measures how re-identifiable a de-identified dataset still is. Records are grouped
/// into equivalence classes by their quasi-identifiers (age band, sex, ZIP prefix…):
/// fields that are harmless alone but identifying together. A record alone in its
/// class is unique, and so is easy to link to an outside dataset.
///
/// Reports k-anonymity (the smallest class), l-diversity (the fewest distinct sensitive
/// values in any class) and prosecutor risk (1 / class size). It measures; it does not
/// generalize the data to reach a target. Use it before releasing a dataset, alongside
/// (not instead of) an expert's judgment.
/// </summary>
public static class ReidentificationRiskAssessor
{
    /// <param name="records">Each record as field name → value; a missing field counts as null.</param>
    /// <param name="quasiIdentifiers">The fields an attacker could know from elsewhere.</param>
    /// <param name="sensitiveAttribute">The field to check for l-diversity (e.g. diagnosis), or null.</param>
    public static RiskReport Assess(
        IEnumerable<IReadOnlyDictionary<string, string?>> records,
        IReadOnlyList<string> quasiIdentifiers,
        string? sensitiveAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(quasiIdentifiers);
        if (quasiIdentifiers.Count == 0 || quasiIdentifiers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Name at least one quasi-identifier, none blank.", nameof(quasiIdentifiers));
        }

        if (quasiIdentifiers.Distinct(StringComparer.Ordinal).Count() != quasiIdentifiers.Count)
        {
            throw new ArgumentException("Quasi-identifiers must be distinct.", nameof(quasiIdentifiers));
        }

        var list = records.ToList();

        // Callers that don't use nullable annotations can still pass a null.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (list.Any(r => r is null))
        {
            throw new ArgumentException("Records cannot be null.", nameof(records));
        }

        if (list.Count == 0)
        {
            return new RiskReport(0, 0, [], null);
        }

        var classes = list
            .GroupBy(r => new ClassKey([.. quasiIdentifiers.Select(r.GetValueOrDefault)]))
            .Select(g => new EquivalenceClass(
                g.Count(),
                sensitiveAttribute is null ? null : g.Select(r => r.GetValueOrDefault(sensitiveAttribute)).Distinct(StringComparer.Ordinal).Count()))
            .ToList();

        return new RiskReport(
            list.Count,
            classes.Count,
            [.. classes.Select(c => c.Size).Order()],
            sensitiveAttribute is null ? null : classes.Min(c => c.DistinctSensitiveValues));
    }

    private sealed record EquivalenceClass(int Size, int? DistinctSensitiveValues);

    // Value equality over the quasi-identifier values (null is a value of its own).
    private sealed class ClassKey(string?[] values) : IEquatable<ClassKey>
    {
        private readonly string?[] _values = values;

        public bool Equals(ClassKey? other) =>
            other is not null && _values.AsSpan().SequenceEqual(other._values, StringComparer.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as ClassKey);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var value in _values)
            {
                hash.Add(value, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
