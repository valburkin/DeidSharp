using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Predictions;

namespace DeidSharp.Evaluation.Cli.Tests.Predictions;

public class PredictionsFileTests
{
    private static readonly CorpusDocument[] Corpus =
    [
        new("patient 1, note 1", "Dr. Hart saw Rosa.", []),
        new("patient 1, note 2", "Rosa slept.", []),
    ];

    private static PredictionsFile Read(string content)
    {
        using var temp = new TempDirectory();
        return PredictionsFile.Read("deid", temp.File("deid.tsv", content), Corpus);
    }

    [Fact]
    public async Task Reads_spans_by_document_and_skips_comments()
    {
        var file = Read("# deid 1.1\n\npatient 1, note 1\t4\t8\tName\npatient 1, note 1\t13\t17\n");
        var ct = TestContext.Current.CancellationToken;

        var spans = await file.PredictAsync(Corpus[0], ct);

        Assert.Equal([new PhiSpan(4, 8, PhiCategory.Name, 1, "deid"), new PhiSpan(13, 17, PhiCategory.OtherIdentifier, 1, "deid")], spans);
        Assert.Empty(await file.PredictAsync(Corpus[1], ct));
        Assert.Equal("deid", file.Name);
    }

    [Theory]
    [InlineData("patient 1, note 1\t4\n", "line 1 is not")]
    [InlineData("patient 1, note 1\t-4\t8\n", "line 1 is not")]
    [InlineData("patient 9, note 1\t4\t8\n", "line 1 names a document the corpus doesn't have")]
    [InlineData("# header\npatient 1, note 2\t4\t99\n", "line 2 has a span that doesn't fit")]
    [InlineData("patient 1, note 1\t8\t8\n", "line 1 has a span that doesn't fit")]
    [InlineData("patient 1, note 1\t4\t8\t3\n", "line 1 has a category DeidSharp doesn't know")]
    [InlineData("patient 1, note 1\t4\t8\tname\n", "line 1 has a category DeidSharp doesn't know")]
    public void Refuses_a_line_it_cannot_score_and_names_only_its_number(string content, string message)
    {
        var e = Assert.Throws<UsageException>(() => Read(content));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }
}
