using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DeidSharp.Evaluation.Cli.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Corpora;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one ICorpus, the reader that builds a surrogate corpus from MIMIC-IV-Note's
// discharge summaries. The scorer runs whichever corpus the command line names, so a new
// corpus is a new class.

/// <summary>
/// Discharge summaries from MIMIC-IV-Note 2.2 (Beth Israel Deaconess, 2008 to 2019), with
/// each <c>___</c> its de-identification left filled by a made-up surrogate that becomes a
/// gold span (<see cref="SurrogateFiller"/>). The notes need a credentialed PhysioNet
/// account and the dataset's data use agreement; the corpus is rebuilt from
/// <c>note/discharge.csv.gz</c> on each run and never written out, except by <c>--export</c>.
/// </summary>
/// <remarks>
/// <para>
/// A fixed sample: the <see cref="SamplePatients"/> patients that come first in the order of a
/// hash of their number, and of each, the note that comes first in the order of a hash of its
/// id. The sample doesn't depend on the file's row order, and the surrogates are drawn from a
/// generator seeded by each note's id, so anyone with the file rebuilds the same corpus.
/// </para>
/// <para>
/// The file is checked against MIMIC-IV-Note 2.2's published SHA-256 before it is read, and
/// the surrogate lists against the hashes generator <see cref="GeneratorVersion"/> was built with.
/// </para>
/// </remarks>
/// <param name="expectedSha256">The file's SHA-256 in hex; null to skip the check (tests).</param>
/// <param name="surrogatesDirectory">Where the surrogate lists are; null for the copies that ship with this tool.</param>
internal sealed class MimicIvNoteCorpus(
    string notesPath,
    int patients = MimicIvNoteCorpus.SamplePatients,
    string? expectedSha256 = MimicIvNoteCorpus.ReleaseSha256,
    string? surrogatesDirectory = null) : ICorpus
{
    /// <summary>How many patients (one note each) the corpus samples.</summary>
    private const int SamplePatients = 1_000;

    /// <summary>SHA-256 of <c>note/discharge.csv.gz</c> in MIMIC-IV-Note 2.2, from the release's SHA256SUMS.txt.</summary>
    public const string ReleaseSha256 = "27bc552edaf81c2af322040cb7e8eb36417173b23c79405bb92fcf73bef2dbc5";

    /// <summary>
    /// Changes whenever a change to the rules, the lists or the drawing would change the
    /// corpus, so results from two versions are never compared as if from one.
    /// </summary>
    public const int GeneratorVersion = 3;

    private static readonly string[] Columns = ["note_id", "subject_id", "text"];

    public string Id => "mimic-iv-note";

    public string Title => $"MIMIC-IV-Note 2.2 discharge summaries with surrogate PHI (generator {GeneratorVersion})";

    public LabelMap Labels { get; } = new(
    [
        new LabelRule(SurrogateLabels.PatientName, PhiCategory.Name, SafeHarbor: true),
        new LabelRule(SurrogateLabels.StaffName, PhiCategory.Name, SafeHarbor: true),
        new LabelRule(SurrogateLabels.RelativeName, PhiCategory.Name, SafeHarbor: true),
        new LabelRule(SurrogateLabels.UntypedName, PhiCategory.Name, SafeHarbor: true),
        new LabelRule(SurrogateLabels.Date, PhiCategory.Date, SafeHarbor: true),
        new LabelRule(SurrogateLabels.Age, PhiCategory.Age, SafeHarbor: true),
        // MIMIC removed every age; Safe Harbor, and so DeidSharp, keeps those under 90.
        new LabelRule(SurrogateLabels.AgeUnder90, PhiCategory.Age, SafeHarbor: false),
        new LabelRule(SurrogateLabels.RecordNumber, PhiCategory.MedicalRecordNumber, SafeHarbor: true),
        new LabelRule(SurrogateLabels.Phone, PhiCategory.Phone, SafeHarbor: true),
        new LabelRule(SurrogateLabels.Hospital, PhiCategory.Location, SafeHarbor: true),
        new LabelRule(SurrogateLabels.City, PhiCategory.Location, SafeHarbor: true),
    ]);

    public IReadOnlyList<CorpusDocument> Read()
    {
        if (expectedSha256 is not null)
        {
            EnsureRelease(expectedSha256);
        }

        var lists = surrogatesDirectory is null ? SurrogateLists.LoadBundled() : SurrogateLists.Load(surrogatesDirectory);
        return [.. Sample().Select(note => Fill(note, lists))];
    }

    private void EnsureRelease(string expected)
    {
        using var file = File.OpenRead(notesPath);
        if (!Convert.ToHexStringLower(SHA256.HashData(file)).Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new CorpusFormatException("--notes is not MIMIC-IV-Note 2.2's note/discharge.csv.gz: its SHA-256 differs from the release's. Use the unmodified file.");
        }
    }

    // The patients with the smallest hashes, and each one's note with the smallest hash.
    // Keys only ever fall out of the set, so a patient once dropped never needs to come back.
    private List<Note> Sample()
    {
        var chosen = new Dictionary<string, Note>(StringComparer.Ordinal);
        var order = new SortedSet<(ulong Key, string Patient)>(Comparer<(ulong Key, string Patient)>.Create(
            (a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : string.CompareOrdinal(a.Patient, b.Patient)));
        foreach (var (noteId, patient, text) in Notes())
        {
            if (chosen.TryGetValue(patient, out var held))
            {
                var noteKey = Hash("deidsharp-mimic-note", noteId);
                if (noteKey < held.NoteKey)
                {
                    chosen[patient] = held with { Id = noteId, NoteKey = noteKey, Text = text };
                }

                continue;
            }

            var patientKey = Hash("deidsharp-mimic-patient", patient);
            if (order.Count == patients && patientKey >= order.Max.Key)
            {
                continue;
            }

            chosen[patient] = new Note(noteId, patient, Hash("deidsharp-mimic-note", noteId), text);
            order.Add((patientKey, patient));
            if (order.Count <= patients)
            {
                continue;
            }

            var dropped = order.Max;
            order.Remove(dropped);
            chosen.Remove(dropped.Patient);
        }

        return order.Count == patients
            ? [.. order.Select(o => chosen[o.Patient])]
            : throw new CorpusFormatException($"The file holds {order.Count:N0} patients, fewer than the {patients:N0} the sample needs.");
    }

    private IEnumerable<(string NoteId, string Patient, string Text)> Notes()
    {
        using var file = File.OpenRead(notesPath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        int[]? at = null;
        var needed = 0;
        var record = 0;
        foreach (var fields in new CsvRecordReader(reader).Records())
        {
            record++;
            if (at is null)
            {
                at = [.. Columns.Select(c => IndexOf(fields, c))];
                needed = at.Max() + 1;
                continue;
            }

            // Messages name the record's number, never its fields: they hold note text.
            if (fields.Count < needed)
            {
                throw new CorpusFormatException($"Record {record} of --notes has {fields.Count} fields, too few for its header.");
            }

            yield return (fields[at[0]], fields[at[1]], fields[at[2]]);
        }

        if (at is null)
        {
            throw new CorpusFormatException("--notes is empty.");
        }
    }

    private static int IndexOf(IReadOnlyList<string> header, string column)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (header[i] == column)
            {
                return i;
            }
        }

        throw new CorpusFormatException($"--notes has no {column} column; it should be MIMIC-IV-Note's note/discharge.csv.gz.");
    }

    private static CorpusDocument Fill(Note note, SurrogateLists lists)
    {
        var random = SeededRandom.For($"deidsharp-mimic-surrogates-{GeneratorVersion}", note.Id);
        var (text, spans) = SurrogateFiller.Fill(note.Text, new NoteSurrogates(lists, random, SurrogateFiller.Sex(note.Text)));
        return new CorpusDocument($"note {note.Id}", text, spans, $"patient {note.Patient}");
    }

    private static ulong Hash(string purpose, string key) =>
        BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(purpose + ":" + key)));

    private sealed record Note(string Id, string Patient, ulong NoteKey, string Text);
}
