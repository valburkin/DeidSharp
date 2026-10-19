namespace DeidSharp.Risk;

/// <summary>Re-identification risk of a dataset (see <see cref="ReidentificationRiskAssessor"/>).</summary>
/// <param name="ClassSizes">The size of every equivalence class, smallest first.</param>
/// <param name="MinimumDistinctSensitiveValues">The dataset's l (l-diversity); null when no sensitive attribute was given.</param>
public sealed record RiskReport(
    int RecordCount,
    int EquivalenceClassCount,
    IReadOnlyList<int> ClassSizes,
    int? MinimumDistinctSensitiveValues)
{
    /// <summary>The dataset's k: the size of its smallest equivalence class (0 when empty).</summary>
    public int MinimumClassSize => ClassSizes.Count == 0 ? 0 : ClassSizes[0];

    /// <summary>Records that are alone in their class.</summary>
    public int UniqueRecords => ClassSizes.Count(s => s == 1);

    /// <summary>The highest chance of correctly re-identifying one record: 1 / k (0 when empty).</summary>
    public double MaximumProsecutorRisk => MinimumClassSize == 0 ? 0 : 1.0 / MinimumClassSize;

    /// <summary>The average chance over all records: classes / records (0 when empty).</summary>
    public double AverageProsecutorRisk => RecordCount == 0 ? 0 : (double)EquivalenceClassCount / RecordCount;

    /// <summary>Records in classes smaller than <paramref name="k"/>: the ones that break k-anonymity.</summary>
    public int RecordsInClassesSmallerThan(int k)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        return ClassSizes.Where(s => s < k).Sum();
    }

    /// <summary>True if every class holds at least <paramref name="k"/> records (an empty dataset is).</summary>
    public bool IsKAnonymous(int k)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        return RecordCount == 0 || MinimumClassSize >= k;
    }

    /// <summary>True if every class holds at least <paramref name="l"/> distinct sensitive values.</summary>
    /// <exception cref="InvalidOperationException">No sensitive attribute was assessed.</exception>
    public bool IsLDiverse(int l)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(l, 1);
        if (RecordCount == 0)
        {
            return true;
        }

        return MinimumDistinctSensitiveValues is { } diversity
            ? diversity >= l
            : throw new InvalidOperationException("l-diversity needs a sensitive attribute; none was assessed.");
    }
}
