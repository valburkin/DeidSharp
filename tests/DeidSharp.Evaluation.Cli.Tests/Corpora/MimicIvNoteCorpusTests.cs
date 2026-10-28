using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Corpora.Surrogates;
using DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora;

/// <summary>The MIMIC-IV-Note reader on a made-up file in the release's format.</summary>
public sealed class MimicIvNoteCorpusTests : IDisposable
{
    private const string Header = "note_id,subject_id,hadm_id,note_type,note_seq,charttime,storetime,text";

    // SHA-256 of the fixed file's corpus, built by generator 3 with the shipped lists.
    private const string GoldenSha256 = "9fffe399882cf149bf58eb25b6ebd25c67df735243cc108e45d80c1c545862d0";

    private static readonly string[] Rows =
    [
        Row("10-DS-1", "10", "Name:  ___  Unit No:   ___\nSeen by Dr. ___, then \"cleared\"."),
        Row("10-DS-2", "10", "Ms. ___ was admitted on ___."),
        Row("11-DS-1", "11", "No placeholders here."),
        Row("12-DS-1", "12", "Transferred from ___ for care."),
        Row("13-DS-1", "13", "Her son ___ visited."),
    ];

    private readonly TempDirectory _files = new();
    private readonly string _lists;

    public MimicIvNoteCorpusTests() => _lists = SurrogateListsFixture.Write(_files);

    public void Dispose() => _files.Dispose();

    private static string Row(string noteId, string patient, string text) =>
        $"{noteId},{patient},20000,DS,1,2150-01-01 00:00:00,2150-01-02 00:00:00,\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private string Gzip(string name, IEnumerable<string> lines)
    {
        var path = Path.Combine(_files.Path, name);
        using var gzip = new GZipStream(File.Create(path), CompressionLevel.Fastest);
        gzip.Write(Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));
        return path;
    }

    private MimicIvNoteCorpus Corpus(string path, int patients = 3, string? sha256 = null) => new(path, patients, sha256, _lists);

    // The order the sample documents, computed here on its own: the first 8 bytes of SHA-256, big-endian.
    private static ulong Key(string purpose, string id) =>
        BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{id}")));

    [Fact]
    public void Samples_one_note_for_each_patient_whatever_the_row_order()
    {
        var forward = Corpus(Gzip("forward.csv.gz", [Header, .. Rows])).Read();
        var backward = Corpus(Gzip("backward.csv.gz", [Header, .. Rows.Reverse()])).Read();

        Assert.Equal(3, forward.Count);
        Assert.Equal(3, forward.Select(d => d.SplitGroup).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(forward.Select(d => (d.Id, d.Text)), backward.Select(d => (d.Id, d.Text)));
        Assert.All(forward, d => Assert.StartsWith("patient ", d.SplitGroup, StringComparison.Ordinal));
    }

    [Fact]
    public void Samples_the_patients_and_notes_with_the_smallest_hashes_as_documented()
    {
        string[] many =
        [
            .. Enumerable.Range(100, 30).SelectMany(p => Enumerable.Range(1, p % 3 + 1).Select(n => Row($"{p}-DS-{n}", $"{p}", $"Note {n}."))),
        ];
        var expected = Enumerable.Range(100, 30)
            .Select(p => $"{p}")
            .OrderBy(p => Key("deidsharp-mimic-patient", p))
            .Take(12)
            .Select(p => Enumerable.Range(1, int.Parse(p, CultureInfo.InvariantCulture) % 3 + 1).Select(n => $"{p}-DS-{n}").MinBy(id => Key("deidsharp-mimic-note", id)))
            .Select(id => $"note {id}");

        var documents = Corpus(Gzip("many.csv.gz", [Header, .. many]), patients: 12).Read();

        Assert.Equal(expected, documents.Select(d => d.Id));
    }

    [Fact]
    public void Builds_a_fixed_file_exactly_as_this_generator_version_does()
    {
        // Pins the whole corpus build with the shipped lists: the sample, the seed and the
        // surrogates. A change to any of them must come with a new GeneratorVersion.
        var documents = new MimicIvNoteCorpus(Gzip("notes.csv.gz", [Header, .. Rows]), patients: 4, expectedSha256: null).Read();
        var output = string.Concat(documents.Select(d => $"{d.Id}|{d.SplitGroup}|{d.Text}|{string.Join(";", d.Spans.Select(s => $"{s.Start} {s.End} {s.Label}"))}\n"));

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(output)));

        Assert.True(hash == GoldenSha256, $"The corpus changed: bump MimicIvNoteCorpus.GeneratorVersion, then set {nameof(GoldenSha256)} to {hash}.");
    }

    [Fact]
    public void Fills_every_placeholder_with_labels_the_map_lists()
    {
        var corpus = Corpus(Gzip("notes.csv.gz", [Header, .. Rows]), patients: 4);

        var documents = corpus.Read();

        Assert.All(documents, d => Assert.DoesNotContain(SurrogateFiller.Placeholder, d.Text, StringComparison.Ordinal));
        Assert.All(documents, d => corpus.Labels.ToAnnotated(d, EvaluationScope.All));
        Assert.Contains(documents, d => d.Spans.Count > 0);
    }

    [Fact]
    public void Finds_its_columns_by_name()
    {
        var reordered = Rows
            .Select(r => r.Split(',', 3))
            .Select(f => $"{f[2]},{f[1]},{f[0]}")
            .Prepend("hadm_id,note_type,note_seq,charttime,storetime,text,subject_id,note_id");

        Assert.Equal(3, Corpus(Gzip("reordered.csv.gz", reordered)).Read().Count);
    }

    [Fact]
    public void Refuses_a_file_that_is_not_the_release()
    {
        var path = Gzip("notes.csv.gz", [Header, .. Rows]);
        string own;
        using (var file = File.OpenRead(path))
        {
            own = Convert.ToHexStringLower(SHA256.HashData(file));
        }

        var e = Assert.Throws<CorpusFormatException>(() => Corpus(path, sha256: MimicIvNoteCorpus.ReleaseSha256).Read());

        Assert.Contains("SHA-256 differs", e.Message, StringComparison.Ordinal);
        Assert.Equal(3, Corpus(path, sha256: own).Read().Count);
    }

    [Fact]
    public void Refuses_a_file_without_a_column_it_needs()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Gzip("notes.csv.gz", ["note_id,text", "1,x"])).Read());

        Assert.Contains("no subject_id column", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_short_record_by_its_number_without_quoting_it()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Gzip("notes.csv.gz", [Header, Rows[0], "10-DS-9,Hart"])).Read());

        Assert.Contains("Record 3", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_blank_record_by_its_number()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Gzip("notes.csv.gz", [Header, Rows[0], "", Rows[2]])).Read());

        Assert.Contains("Record 3", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_file_with_fewer_patients_than_the_sample()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(Gzip("notes.csv.gz", [Header, .. Rows]), patients: 5).Read());

        Assert.Contains("holds 4 patients, fewer than the 5", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_its_generator_version_in_its_title()
    {
        var corpus = Corpus("unused");

        Assert.Equal("mimic-iv-note", corpus.Id);
        Assert.Contains($"generator {MimicIvNoteCorpus.GeneratorVersion}", corpus.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_an_age_under_90_out_of_the_safe_harbor_scope()
    {
        var corpus = Corpus("unused");
        var note = new CorpusDocument("note 1", "72 y/o F, age 93", [new LabeledSpan(0, 2, SurrogateLabels.AgeUnder90), new LabeledSpan(14, 16, SurrogateLabels.Age)]);

        Assert.Single(corpus.Labels.ToAnnotated(note, EvaluationScope.SafeHarbor).Gold);
        Assert.Equal(2, corpus.Labels.ToAnnotated(note, EvaluationScope.All).Gold.Count);
    }
}
