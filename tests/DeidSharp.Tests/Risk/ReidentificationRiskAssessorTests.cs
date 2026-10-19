using DeidSharp.Risk;

namespace DeidSharp.Tests.Risk;

public class ReidentificationRiskAssessorTests
{
    private static readonly string[] Qis = ["ageBand", "sex", "zip3"];

    private static Dictionary<string, string?> Row(string? age, string? sex, string? zip, string? dx = null) =>
        new() { ["ageBand"] = age, ["sex"] = sex, ["zip3"] = zip, ["dx"] = dx };

    [Fact]
    public void An_empty_dataset_has_no_risk_and_is_vacuously_anonymous()
    {
        var report = ReidentificationRiskAssessor.Assess([], Qis, "dx");
        Assert.Equal(0, report.RecordCount);
        Assert.Equal(0, report.MinimumClassSize);
        Assert.Equal(0, report.MaximumProsecutorRisk);
        Assert.Equal(0, report.AverageProsecutorRisk);
        Assert.True(report.IsKAnonymous(5));
        Assert.True(report.IsLDiverse(2));
    }

    [Fact]
    public void Groups_records_by_their_quasi_identifiers()
    {
        var report = ReidentificationRiskAssessor.Assess(
        [
            Row("60s", "F", "021"), Row("60s", "F", "021"), Row("60s", "F", "021"),
            Row("70s", "M", "021"), Row("70s", "M", "021"),
            Row("90+", "F", "000"),
        ], Qis);

        Assert.Equal(6, report.RecordCount);
        Assert.Equal(3, report.EquivalenceClassCount);
        Assert.Equal([1, 2, 3], report.ClassSizes);
        Assert.Equal(1, report.MinimumClassSize);
        Assert.Equal(1, report.UniqueRecords);
        Assert.Equal(1.0, report.MaximumProsecutorRisk);
        Assert.Equal(0.5, report.AverageProsecutorRisk);
        Assert.True(report.IsKAnonymous(1));
        Assert.False(report.IsKAnonymous(2));
        Assert.Equal(3, report.RecordsInClassesSmallerThan(3));
    }

    [Fact]
    public void A_null_or_missing_value_is_a_value_of_its_own()
    {
        var report = ReidentificationRiskAssessor.Assess(
        [
            Row(null, "F", "021"),
            Row(null, "F", "021"),
            new Dictionary<string, string?> { ["sex"] = "F", ["zip3"] = "021" },
            Row("60s", "F", "021"),
        ], Qis);

        Assert.Equal([1, 3], report.ClassSizes);
    }

    [Fact]
    public void Values_are_compared_exactly()
    {
        var report = ReidentificationRiskAssessor.Assess([Row("60s", "F", "021"), Row("60S", "f", "021")], Qis);
        Assert.Equal(2, report.EquivalenceClassCount);
    }

    [Fact]
    public void Measures_l_diversity_of_a_sensitive_attribute()
    {
        var report = ReidentificationRiskAssessor.Assess(
        [
            Row("60s", "F", "021", "CKD"), Row("60s", "F", "021", "CKD"),
            Row("70s", "M", "021", "CKD"), Row("70s", "M", "021", "HTN"),
        ], Qis, "dx");

        Assert.Equal(1, report.MinimumDistinctSensitiveValues);
        Assert.True(report.IsKAnonymous(2));
        Assert.False(report.IsLDiverse(2));
    }

    [Fact]
    public void L_diversity_needs_a_sensitive_attribute()
    {
        var report = ReidentificationRiskAssessor.Assess([Row("60s", "F", "021")], Qis);
        Assert.Null(report.MinimumDistinctSensitiveValues);
        Assert.Throws<InvalidOperationException>(() => report.IsLDiverse(2));
    }

    [Fact]
    public void Rejects_bad_arguments()
    {
        Assert.Throws<ArgumentException>(() => ReidentificationRiskAssessor.Assess([Row("a", "b", "c")], []));
        Assert.Throws<ArgumentException>(() => ReidentificationRiskAssessor.Assess([Row("a", "b", "c")], ["sex", "sex"]));
        Assert.Throws<ArgumentException>(() => ReidentificationRiskAssessor.Assess([Row("a", "b", "c")], [" "]));
        Assert.Throws<ArgumentException>(() => ReidentificationRiskAssessor.Assess([null!], Qis));
        var report = ReidentificationRiskAssessor.Assess([Row("a", "b", "c")], Qis);
        Assert.Throws<ArgumentOutOfRangeException>(() => report.IsKAnonymous(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.RecordsInClassesSmallerThan(0));
    }
}
