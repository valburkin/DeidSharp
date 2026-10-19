using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora;

/// <summary>The PhysioNet reader on fictional notes in the corpus's own format.</summary>
public sealed class PhysioNetDeidCorpusTests : IDisposable
{
    private const string Notes =
        "START_OF_RECORD=1||||1||||\n" +
        "Pt seen by Dr. Hart.\n" +
        "Daughter Carmen called in 2019.\n" +
        "||||END_OF_RECORD\n" +
        "START_OF_RECORD=1||||2||||\n" +
        "No events overnight.\n" +
        "||||END_OF_RECORD\n";

    // "Hart" is at 15 in the first note; the second line starts at 21, so "Carmen" is at 30.
    private const string Gold =
        "1 1 15 19 HCPName Hart\n" +
        "1 1 30 36 RelativeProxyName Carmen\n" +
        "1 1 47 51 DateYear 2019\n";

    private readonly TempDirectory _files = new();

    public void Dispose() => _files.Dispose();

    // The fixtures' own span count stands in for the release's 1,779.
    private PhysioNetDeidCorpus Corpus(string notes, string gold, int? expected = null) =>
        new(_files.File("id.text", notes), _files.File("id-phi.phrase", gold), expected ?? gold.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

    [Fact]
    public void Rebuilds_each_note_as_the_deid_software_counts_it()
    {
        var documents = Corpus(Notes, Gold).Read();

        Assert.Equal(2, documents.Count);
        Assert.Equal("Pt seen by Dr. Hart.\nDaughter Carmen called in 2019.\n", documents[0].Text);
        Assert.Equal("patient 1, note 1", documents[0].Id);
        Assert.Equal(
            [new LabeledSpan(15, 19, "HCPName"), new LabeledSpan(30, 36, "RelativeProxyName"), new LabeledSpan(47, 51, "DateYear")],
            documents[0].Spans);
        Assert.Empty(documents[1].Spans);
    }

    [Fact]
    public void Keeps_a_carriage_return_in_the_text_as_the_deid_software_does()
    {
        // "Seen today\r" plus "\n" is 12 characters, so "Hart" on the next line is at 16.
        var documents = Corpus("START_OF_RECORD=2||||1||||\r\nSeen today\r\nDr. Hart\r\n||||END_OF_RECORD\r\n", "2 1 16 20 HCPName Hart\n").Read();

        Assert.Equal("Seen today\r\nDr. Hart\r\n", documents[0].Text);
        Assert.Equal(new LabeledSpan(16, 20, "HCPName"), Assert.Single(documents[0].Spans));
    }

    [Fact]
    public void Refuses_spans_that_do_not_match_the_text_and_names_lines_not_text()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Notes, "1 1 14 18 HCPName Hart\n1 1 30 36 RelativeProxyName Carmen\n").Read());

        Assert.Contains("1 gold spans don't match", e.Message, StringComparison.Ordinal);
        Assert.Contains("line 1", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_label_the_map_does_not_list()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Notes, "1 1 15 19 Doctor Hart\n").Read());

        Assert.Contains("line 1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_span_for_a_note_the_text_does_not_hold()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Notes, "9 1 15 19 HCPName Hart\n").Read());

        Assert.Contains("patient 9, note 1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_gold_file_with_fewer_spans_than_the_release()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Notes, Gold, expected: 4).Read());

        Assert.Contains("holds 3 gold spans, not the 4 expected", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Expects_the_release_count_by_default()
    {
        var e = Assert.Throws<CorpusFormatException>(() => new PhysioNetDeidCorpus(_files.File("id.text", Notes), _files.File("id-phi.phrase", Gold)).Read());

        Assert.Contains("not the 1,779 expected", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_record_without_an_end()
    {
        Assert.Throws<CorpusFormatException>(() => Corpus("START_OF_RECORD=1||||1||||\nPt seen by Dr. Hart.\n", Gold).Read());
    }

    [Fact]
    public void Leaves_a_year_alone_out_of_the_safe_harbor_scope()
    {
        var corpus = Corpus(Notes, Gold);
        var note = corpus.Read()[0];

        Assert.Equal(2, corpus.Labels.ToAnnotated(note, EvaluationScope.SafeHarbor).Gold.Count);
        Assert.Equal(3, corpus.Labels.ToAnnotated(note, EvaluationScope.All).Gold.Count);
    }
}
