using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Predictions;

namespace DeidSharp.Evaluation.Cli.Tests.Predictions;

public class DeidPhiPredictionsTests
{
    private static readonly CorpusDocument[] Corpus =
    [
        new("patient 1, note 1", "Dr. Hart saw Rosa.", []),
        new("patient 12, note 3", "Rosa slept.", []),
    ];

    private static PredictionsFile Read(string content)
    {
        using var temp = new TempDirectory();
        return PredictionsFile.Read("deid", temp.File("id.phi", content), Corpus);
    }

    [Fact]
    public async Task Reads_deid_output_as_it_comes()
    {
        // deid writes a blank line before each note's header, and no category.
        var file = Read("\nPatient 1\tNote 1\n4\t4\t8\n13\t13\t17\nPatient 12\tNote 3\n0\t0\t4");
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(
            [new PhiSpan(4, 8, PhiCategory.OtherIdentifier, 1, "deid"), new PhiSpan(13, 17, PhiCategory.OtherIdentifier, 1, "deid")],
            await file.PredictAsync(Corpus[0], ct));
        Assert.Equal([new PhiSpan(0, 4, PhiCategory.OtherIdentifier, 1, "deid")], await file.PredictAsync(Corpus[1], ct));
    }

    [Theory]
    [InlineData("4\t4\t8\n", "line 1 is not deid's")]
    [InlineData("Patient 1\tNote 1\n4\t5\t8\n", "line 2 is not deid's")]
    [InlineData("Patient 1\tNote 1\nHart\n", "line 2 is not deid's")]
    [InlineData("Patient 1\tNote 9\n4\t4\t8\n", "line 2 names a document the corpus doesn't have")]
    [InlineData("Patient 12\tNote 3\n4\t4\t99\n", "line 2 has a span that doesn't fit")]
    public void Refuses_a_line_it_cannot_score_and_names_only_its_number(string content, string message)
    {
        var e = Assert.Throws<UsageException>(() => Read(content));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }
}
