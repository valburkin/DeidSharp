using System.Security.Cryptography;
using DeidSharp.Evaluation.Cli.Corpora;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora;

/// <summary>The ASQ-PHI reader, on fictional queries and on the bundled copy.</summary>
public sealed class AsqPhiCorpusTests : IDisposable
{
    private readonly TempDirectory _files = new();

    public void Dispose() => _files.Dispose();

    private AsqPhiCorpus Corpus(string content) => new(_files.File("queries.txt", content));

    [Fact]
    public void Finds_every_occurrence_of_each_tagged_value()
    {
        var documents = Corpus(
            """
            ===QUERY===
            Can Rosa A. take lisinopril? Rosa A. was seen at Elm Clinic.
            ===PHI_TAGS===
            {"identifier_type": "NAME", "value": "Rosa A."}
            {"identifier_type": "GEOGRAPHIC_LOCATION", "value": "Elm Clinic"}

            ===QUERY===
            Guidelines for ACE inhibitors in CKD?
            ===PHI_TAGS===

            """).Read();

        Assert.Equal(2, documents.Count);
        Assert.Equal("Can Rosa A. take lisinopril? Rosa A. was seen at Elm Clinic.", documents[0].Text);
        Assert.Equal(
            [new LabeledSpan(4, 11, "NAME"), new LabeledSpan(29, 36, "NAME"), new LabeledSpan(49, 59, "GEOGRAPHIC_LOCATION")],
            documents[0].Spans);
        Assert.Empty(documents[1].Spans);
    }

    [Fact]
    public void Matches_curly_and_straight_apostrophes_alike()
    {
        var document = Corpus(
            """
            ===QUERY===
            Treated at Children’s Clinic last week.
            ===PHI_TAGS===
            {"identifier_type": "GEOGRAPHIC_LOCATION", "value": "Children's Clinic"}
            """).Read()[0];

        Assert.Equal(new LabeledSpan(11, 28, "GEOGRAPHIC_LOCATION"), Assert.Single(document.Spans));
    }

    [Fact]
    public void Refuses_a_value_missing_from_its_query_without_quoting_either()
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus(
            """
            ===QUERY===
            Seen by Dr. Hart.
            ===PHI_TAGS===
            {"identifier_type": "NAME", "value": "Okafor"}
            """).Read());

        Assert.Contains("Query 1", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Okafor", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"identifier_type\": \"NAME\", \"value\": \"Hart\"")]
    [InlineData("{\"type\": \"NAME\", \"value\": \"Hart\"}")]
    [InlineData("{\"identifier_type\": \"DOCTOR\", \"value\": \"Hart\"}")]
    public void Refuses_a_malformed_tag_by_its_line_number(string tag)
    {
        var e = Assert.Throws<CorpusFormatException>(() => Corpus($"===QUERY===\nSeen by Dr. Hart.\n===PHI_TAGS===\n{tag}\n").Read());

        Assert.Contains("Line 4", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Hart", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_bundled_copy_is_the_published_file()
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(AsqPhiCorpus.BundledPath)));

        // The checksum in data/asq-phi/README.md.
        Assert.Equal("cf00e424b8d2347d019f9f34e2ad1510cb4d853605410f8314bef44df8021fc8", hash);
    }

    [Fact]
    public void The_bundled_copy_reads_with_every_tag_in_place()
    {
        var documents = new AsqPhiCorpus(AsqPhiCorpus.BundledPath).Read();

        Assert.Equal(1051, documents.Count);
        Assert.Equal(219, documents.Count(d => d.Spans.Count == 0));
        Assert.True(documents.Sum(d => d.Spans.Count) >= 2973);
    }
}
