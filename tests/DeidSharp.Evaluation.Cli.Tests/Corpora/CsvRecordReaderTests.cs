using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora;

/// <summary>The CSV reader on records as RFC 4180 writes them.</summary>
public class CsvRecordReaderTests
{
    private static List<IReadOnlyList<string>> Read(string csv) => [.. new CsvRecordReader(new StringReader(csv)).Records()];

    [Fact]
    public void Splits_plain_records_at_commas_and_line_breaks()
    {
        Assert.Equal([["a", "b", "c"], ["1", "", "3"]], Read("a,b,c\n1,,3\n"));
    }

    [Fact]
    public void Keeps_commas_line_breaks_and_doubled_quotes_inside_quotes()
    {
        Assert.Equal([["id", "text"], ["7", "Seen, then \"sent\"\nhome."]], Read("id,text\n7,\"Seen, then \"\"sent\"\"\nhome.\"\n"));
    }

    [Fact]
    public void Ends_a_record_at_a_carriage_return_and_line_feed()
    {
        Assert.Equal([["a", "b"], ["c", "d"]], Read("a,b\r\nc,d\r\n"));
    }

    [Fact]
    public void Reads_a_last_record_without_a_line_break()
    {
        Assert.Equal([["a", "b"], ["c", ""]], Read("a,b\nc,\"\""));
    }

    [Fact]
    public void Reads_a_quoted_field_longer_than_its_buffer()
    {
        var text = new string('x', 200_000);

        Assert.Equal([["1", text]], Read($"1,\"{text}\"\n"));
    }

    [Fact]
    public void Reads_a_blank_line_as_a_record_of_one_empty_field()
    {
        Assert.Equal([["a", "b"], [""], ["c", "d"]], Read("a,b\n\nc,d\n"));
    }

    [Fact]
    public void Keeps_text_after_a_closing_quote_in_the_field()
    {
        Assert.Equal([["ab", "c"]], Read("\"a\"b,c\n"));
    }

    [Fact]
    public void Refuses_a_file_that_ends_inside_quotes_without_quoting_it()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Read("1,\"Dr. Hart"));

        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }
}
