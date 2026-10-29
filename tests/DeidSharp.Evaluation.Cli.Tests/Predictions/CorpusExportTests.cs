using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Predictions;

namespace DeidSharp.Evaluation.Cli.Tests.Predictions;

public class CorpusExportTests
{
    private static readonly CorpusDocument[] Documents =
    [
        new("query 7", "Seen by Dr. Hart in Fairhaven.", []),
        new("query 9", "Café visit with José, 3/14.", []),
    ];

    [Fact]
    public void Writes_each_document_under_a_key_with_its_id_in_the_index()
    {
        using var temp = new TempDirectory();
        var export = Path.Combine(temp.Path, "export");

        CorpusExport.Write(export, Documents);

        Assert.Equal(["# key\tdocument", "00001\tquery 7", "00002\tquery 9"], File.ReadAllLines(Path.Combine(export, CorpusExport.IndexFile)));
        Assert.Equal(Documents[1].Text, File.ReadAllText(Path.Combine(export, CorpusExport.TextFolder, "00002.txt")));
    }

    [Fact]
    public void Marks_each_text_file_as_utf8_so_philter_reads_the_same_characters()
    {
        using var temp = new TempDirectory();

        CorpusExport.Write(temp.Path, Documents);

        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(Path.Combine(temp.Path, CorpusExport.TextFolder, "00001.txt"))[..3]);
    }

    [Fact]
    public void Writes_deid_records_that_rebuild_the_same_text()
    {
        using var temp = new TempDirectory();
        CorpusDocument[] ascii = [Documents[0], new("query 12", "Lives alone.\nSon calls daily.", [])];
        CorpusExport.Write(temp.Path, ascii);

        // The reader deid's own corpus is read with rebuilds each record as deid does. deid
        // counts bytes, which are characters here; to-predictions.cs maps them for other text.
        var records = new PhysioNetDeidCorpus(Path.Combine(temp.Path, CorpusExport.DeidFile), temp.File("gold.phrase", ""), 0).Read();

        Assert.Equal(["patient 1, note 1", "patient 2, note 1"], records.Select(r => r.Id));
        Assert.Equal([.. ascii.Select(d => d.Text + "\n")], records.Select(r => r.Text));
    }

    [Fact]
    public void Refuses_a_directory_that_already_holds_files()
    {
        using var temp = new TempDirectory();
        temp.File("earlier.txt", "");

        var e = Assert.Throws<UsageException>(() => CorpusExport.Write(temp.Path, Documents));

        Assert.Contains("--export needs a new or empty directory", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_to_write_inside_a_git_working_tree()
    {
        var e = Assert.Throws<UsageException>(() => CorpusExport.Write(RepositoryFiles.Path("export"), Documents));

        Assert.Contains("must point outside any Git working tree", e.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(RepositoryFiles.Path("export")));
    }
}
