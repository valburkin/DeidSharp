using System.Text.Json;
using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Verification;

namespace DeidSharp.Tests.Verification;

public class LeakScannerTests
{
    private static readonly LeakScanner Scanner = LeakScanner.CreateDefault();

    [Fact]
    public void Clean_text_gives_a_clean_report() =>
        Assert.True(Scanner.Scan("Chronic kidney disease stage 3; lisinopril 10 MG Oral Tablet", cancellationToken: TestContext.Current.CancellationToken).IsClean);

    [Fact]
    public void Reports_a_patterned_identifier_with_its_path_and_category()
    {
        var report = Scanner.Scan([new ScannedValue("$.conditions[0]", "CKD, SSN 123-45-6789")], cancellationToken: TestContext.Current.CancellationToken);

        var finding = Assert.Single(report.Findings);
        Assert.Equal("$.conditions[0]", finding.Path);
        Assert.Equal(PhiCategory.Ssn, finding.Category);
        Assert.Equal((9, 20), (finding.Start, finding.End));
    }

    [Fact]
    public void Reports_a_known_identifier_that_no_pattern_would_find()
    {
        var known = KnownIdentifiers.Empty.AddName("Rosa Alvarez");
        var report = Scanner.Scan("allergies: peanuts (per Alvarez)", known, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(PhiCategory.Name, Assert.Single(report.Findings).Category);
    }

    [Fact]
    public void Ignored_categories_and_low_scores_are_not_reported()
    {
        const string text = "see https://example.org and 555-0101";
        Assert.Equal(2, Scanner.Scan(text, cancellationToken: TestContext.Current.CancellationToken).Findings.Count);
        Assert.Single(Scanner.Scan(text, options: new LeakScanOptions { IgnoredCategories = new HashSet<PhiCategory> { PhiCategory.Url } }, cancellationToken: TestContext.Current.CancellationToken).Findings);
        Assert.Single(Scanner.Scan(text, options: new LeakScanOptions { MinimumScore = 0.6 }, cancellationToken: TestContext.Current.CancellationToken).Findings);
    }

    [Fact]
    public void Scans_every_string_in_a_json_document_with_its_path()
    {
        using var doc = JsonDocument.Parse("""
            {"ageBand":"60s","conditions":["CKD","seen 2024-03-05"],"meds":[{"name":"x","dose":"call 617-555-0198"}],"n":5,"b":true,"z":null}
            """);

        var report = Scanner.ScanJson(doc.RootElement, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["$.conditions[1]", "$.meds[0].dose"], report.Findings.Select(f => f.Path));
    }

    [Fact]
    public void Can_scan_json_property_names_too()
    {
        using var doc = JsonDocument.Parse("""{"Rosa Alvarez":{"ok":"fine"}}""");
        var known = KnownIdentifiers.Empty.AddName("Rosa Alvarez");

        Assert.True(Scanner.ScanJson(doc.RootElement, known, cancellationToken: TestContext.Current.CancellationToken).IsClean);
        Assert.Equal("$.Rosa Alvarez(name)", Assert.Single(Scanner.ScanJson(doc.RootElement, known, new LeakScanOptions { ScanPropertyNames = true }, cancellationToken: TestContext.Current.CancellationToken).Findings).Path);
    }

    [Fact]
    public void Property_names_outside_the_allowed_set_are_masked_in_paths()
    {
        using var doc = JsonDocument.Parse("""{"conditions":["ok"],"Ngozi Okafor":"123-45-6789"}""");
        var options = new LeakScanOptions { PathPropertyNames = new HashSet<string> { "conditions" }, ScanPropertyNames = true };

        var report = Scanner.ScanJson(doc.RootElement, KnownIdentifiers.Empty.AddName("Ngozi Okafor"), options, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["$.<property 1>(name)", "$.<property 1>"], report.Findings.Select(f => f.Path));
        Assert.All(report.Describe(), line => Assert.DoesNotContain("Okafor", line, StringComparison.Ordinal));
    }

    [Fact]
    public void The_description_never_contains_the_value()
    {
        var report = Scanner.Scan("SSN 123-45-6789", cancellationToken: TestContext.Current.CancellationToken);
        var line = Assert.Single(report.Describe());
        Assert.DoesNotContain("6789", line, StringComparison.Ordinal);
        Assert.Contains("Ssn", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_findings_are_reported_once()
    {
        var known = KnownIdentifiers.Empty.AddPhone("617-555-0198");
        Assert.Single(Scanner.Scan("call 617-555-0198", known, cancellationToken: TestContext.Current.CancellationToken).Findings);
    }

    [Fact]
    public void Rejects_an_empty_or_null_detector_list()
    {
        Assert.Throws<ArgumentException>(() => new LeakScanner([]));
        Assert.Throws<ArgumentException>(() => new LeakScanner([new KnownIdentifierDetector(), null!]));
    }

    [Fact]
    public void Empty_values_are_skipped() => Assert.True(Scanner.Scan([new ScannedValue("$", "")], cancellationToken: TestContext.Current.CancellationToken).IsClean);

    [Fact]
    public void A_canceled_scan_stops_instead_of_reporting_clean()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Scanner.Scan([new ScannedValue("$.a", "CKD"), new ScannedValue("$.b", "SSN 123-45-6789")], cancellationToken: canceled.Token));
    }
}
