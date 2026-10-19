using System.Text;
using System.Text.Json;

namespace DeidSharp.Evaluation.Cli.Corpora;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one ICorpus, the reader for the ASQ-PHI benchmark. The scorer runs whichever
// corpus the command line names, so a new corpus format is a new class.

/// <summary>
/// ASQ-PHI: 1,051 synthetic clinical search queries, 832 with PHI and 219 hard negatives
/// without, MIT licensed. A copy ships with this tool (data/asq-phi), so CI can measure on
/// it with every change.
/// </summary>
/// <remarks>
/// Each record is a <c>===QUERY===</c> line, the query, a <c>===PHI_TAGS===</c> line, then one
/// JSON object per tag: <c>{"identifier_type": "NAME", "value": "Anna S."}</c>. Tags carry
/// values, not offsets, so every occurrence of a value in its query is a gold span. Curly and
/// straight apostrophes are searched alike: one published value differs from its query only there.
/// </remarks>
internal sealed class AsqPhiCorpus(string path) : ICorpus
{
    private const string QueryMarker = "===QUERY===";
    private const string TagsMarker = "===PHI_TAGS===";

    /// <summary>The copy that ships with this tool.</summary>
    public static string BundledPath { get; } = Path.Combine(AppContext.BaseDirectory, "data", "asq-phi", "synthetic_clinical_queries.txt");

    public string Id => "asq-phi";

    public string Title => "ASQ-PHI (synthetic clinical queries)";

    public LabelMap Labels { get; } = new(
    [
        new LabelRule("NAME", PhiCategory.Name, SafeHarbor: true),
        new LabelRule("GEOGRAPHIC_LOCATION", PhiCategory.Location, SafeHarbor: true),
        new LabelRule("DATE", PhiCategory.Date, SafeHarbor: true),
        new LabelRule("MEDICAL_RECORD_NUMBER", PhiCategory.MedicalRecordNumber, SafeHarbor: true),
        new LabelRule("HEALTH_PLAN_BENEFICIARY_NUMBER", PhiCategory.HealthPlanNumber, SafeHarbor: true),
        new LabelRule("PHONE_NUMBER", PhiCategory.Phone, SafeHarbor: true),
        new LabelRule("FAX_NUMBER", PhiCategory.Fax, SafeHarbor: true),
        new LabelRule("EMAIL_ADDRESS", PhiCategory.Email, SafeHarbor: true),
        new LabelRule("SOCIAL_SECURITY_NUMBER", PhiCategory.Ssn, SafeHarbor: true),
        new LabelRule("ACCOUNT_NUMBER", PhiCategory.AccountNumber, SafeHarbor: true),
        new LabelRule("CERTIFICATE_LICENSE_NUMBER", PhiCategory.LicenseNumber, SafeHarbor: true),
        new LabelRule("IP_ADDRESS", PhiCategory.IpAddress, SafeHarbor: true),
        new LabelRule("UNIQUE_IDENTIFIER", PhiCategory.OtherIdentifier, SafeHarbor: true),
    ]);

    public IReadOnlyList<CorpusDocument> Read()
    {
        var documents = new List<CorpusDocument>();
        var query = new List<string>();
        var tags = new List<(string Label, string Value)>();
        var inTags = false;
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            lineNumber++;
            switch (line)
            {
                case QueryMarker:
                    Finish();
                    inTags = false;
                    continue;
                case TagsMarker:
                    inTags = true;
                    continue;
            }

            if (!inTags)
            {
                query.Add(line);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                tags.Add(ParseTag(line, lineNumber));
            }
        }

        Finish();
        return documents.Count > 0 ? documents : throw new CorpusFormatException("The ASQ-PHI file holds no queries.");

        void Finish()
        {
            var text = string.Join('\n', query).Trim();
            if (text.Length > 0)
            {
                var number = documents.Count + 1;
                documents.Add(new CorpusDocument($"query {number}", text, Align(text, tags, number)));
            }
            else if (tags.Count > 0)
            {
                throw new CorpusFormatException($"Line {lineNumber}: PHI tags without a query.");
            }

            query.Clear();
            tags.Clear();
        }
    }

    private (string Label, string Value) ParseTag(string line, int lineNumber)
    {
        string? label, value;
        try
        {
            using var tag = JsonDocument.Parse(line);
            label = tag.RootElement.GetProperty("identifier_type").GetString();
            value = tag.RootElement.GetProperty("value").GetString();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // The parser's message can quote the line; give the position only.
            throw new CorpusFormatException($"Line {lineNumber}: a PHI tag is not {{\"identifier_type\": …, \"value\": …}}.");
        }

        if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value))
        {
            throw new CorpusFormatException($"Line {lineNumber}: a PHI tag has an empty type or value.");
        }

        return Labels.Knows(label) ? (label, value) : throw new CorpusFormatException($"Line {lineNumber}: a PHI type the corpus's label map doesn't list.");
    }

    private static List<LabeledSpan> Align(string text, List<(string Label, string Value)> tags, int number)
    {
        var searchable = Apostrophes(text);
        var spans = new List<LabeledSpan>();
        foreach (var (label, value) in tags)
        {
            var needle = Apostrophes(value);
            var found = false;
            for (var at = searchable.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = searchable.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
            {
                found = true;
                if (!spans.Exists(s => s.Start == at && s.End == at + needle.Length))
                {
                    spans.Add(new LabeledSpan(at, at + needle.Length, label));
                }
            }

            if (!found)
            {
                throw new CorpusFormatException($"Query {number}: a tagged value doesn't appear in the query.");
            }
        }

        return spans;
    }

    // One character for one, so offsets in the result are offsets in the original.
    private static string Apostrophes(string text) => text.Replace('’', '\'');
}
