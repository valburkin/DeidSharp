using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Detection.Patterns;
using DeidSharp.Text;

namespace DeidSharp.IntegrationTests;

public sealed class ClinicalAnalyzerTests(ClinicalAnalyzerContainer analyzer)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private const string Note = """
        Pt is an 81yo F seen in clinic with her daughter Ngozi Okafor. Dr. Bjornsson (cardiology) was consulted.
        Transferred from Mercy General on 03/04/2024. Lives in Northampton. Return to clinic with Dr Patel in 2 wks.
        """;

    [Fact]
    public async Task Reports_healthy() => Assert.True(await analyzer.Detector("clinical").IsHealthyAsync(Ct));

    [Fact]
    public async Task Finds_patient_relative_and_staff_names_the_record_does_not_list()
    {
        var spans = await analyzer.Detector("clinical").DetectAsync(new DetectionRequest(Note), Ct);
        var names = spans.Where(s => s.Category == PhiCategory.Name).Select(s => Note[s.Start..s.End]).ToList();

        Assert.Contains(names, n => n.Contains("Okafor", StringComparison.Ordinal));
        Assert.Contains(names, n => n.Contains("Bjornsson", StringComparison.Ordinal));
        Assert.Contains(names, n => n.Contains("Patel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Finds_hospitals_and_places()
    {
        var spans = await analyzer.Detector("clinical").DetectAsync(new DetectionRequest(Note), Ct);
        var covered = spans.Select(s => Note[s.Start..s.End]).ToList();

        Assert.Contains(covered, c => c.Contains("Mercy", StringComparison.Ordinal));
        Assert.Contains(covered, c => c.Contains("Northampton", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_full_stack_leaves_none_of_it()
    {
        var stack = new CompositePhiDetector([analyzer.Detector("clinical"), new KnownIdentifierDetector(), new PatternPhiDetector()]);
        var result = await new TextDeidentifier(stack, new TagReplacement()).DeidentifyAsync(Note, cancellationToken: Ct);

        foreach (var secret in new[] { "Ngozi", "Okafor", "Bjornsson", "Mercy", "03/04/2024", "Northampton", "Patel" })
        {
            Assert.DoesNotContain(secret, result.Text, StringComparison.Ordinal);
        }
    }
}
