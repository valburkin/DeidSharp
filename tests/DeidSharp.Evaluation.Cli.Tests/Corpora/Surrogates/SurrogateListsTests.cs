using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

/// <summary>The surrogate lists: the shipped copies pinned to the generator, malformed copies refused.</summary>
public sealed class SurrogateListsTests : IDisposable
{
    private readonly TempDirectory _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public void Loads_the_shipped_lists_the_generator_was_built_with()
    {
        var lists = SurrogateLists.LoadBundled();

        Assert.NotEmpty(lists.Surname(new SeededRandom(1)));
    }

    [Fact]
    public void Hashes_a_list_the_same_whatever_its_line_endings()
    {
        var lf = _files.File("lf.txt", "# header\nquill\t300\n");
        var crlf = _files.File("crlf.txt", "# header\r\nquill\t300\r\n");

        Assert.Equal(SurrogateLists.ContentHash(lf), SurrogateLists.ContentHash(crlf));
    }

    [Theory]
    [InlineData("first-names.tsv", "Ada\tX\t2.5\n", "first-names.tsv line 1")]
    [InlineData("first-names.tsv", "Ada\tF\t-1\n", "first-names.tsv line 1")]
    [InlineData("surnames.txt", "quill\n", "surnames.txt line 1")]
    [InlineData("surnames.txt", "# only a header\n", "is empty")]
    [InlineData("places.txt", "# only a header\n", "places.txt is empty")]
    public void Refuses_a_malformed_or_empty_list_by_its_line(string file, string content, string message)
    {
        SurrogateListsFixture.Write(_files);
        _files.File(file, content);

        var e = Assert.Throws<CorpusFormatException>(() => SurrogateLists.Load(_files.Path));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_surnames_capitalized_and_mc_names_with_a_second_capital()
    {
        SurrogateListsFixture.Write(_files);
        _files.File("surnames.txt", "mcdonald\t100\n");

        Assert.Equal("McDonald", SurrogateLists.Load(_files.Path).Surname(new SeededRandom(1)));
    }
}
