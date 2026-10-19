using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DeidSharp.Evaluation.Cli.Corpora;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one ICorpus, the reader for PhysioNet's nursing-note gold standard. The scorer
// runs whichever corpus the command line names, so a new corpus format is a new class.

/// <summary>
/// PhysioNet's Deidentified Medical Text: 2,434 ICU nursing notes whose PHI was replaced
/// by realistic surrogates. The notes come from <c>id.text</c> (credentialed access, under
/// a data use agreement); the gold spans from <c>id-phi.phrase</c> in PhysioNet's open
/// deid software package.
/// </summary>
/// <remarks>
/// <para>
/// <c>id.text</c> holds records between a <c>START_OF_RECORD=patient||||note||||</c> line and
/// a <c>||||END_OF_RECORD</c> line. The deid software counts offsets in the text it rebuilds
/// from a record: each line without its newline, followed by "\n". This reader rebuilds the
/// text the same way, one character per byte (Latin-1), so the offsets mean the same.
/// </para>
/// <para>
/// <c>id-phi.phrase</c> lines are <c>patient note start end type text</c>, with the end
/// exclusive. Every span's text is checked against the note before anything is scored: a
/// mismatch means the two files don't belong together.
/// </para>
/// </remarks>
/// <param name="expectedGoldSpans">How many spans the gold file must hold; null for deid 1.1's <see cref="ReleaseGoldSpans"/>.</param>
internal sealed partial class PhysioNetDeidCorpus(string textPath, string goldPath, int? expectedGoldSpans = null) : ICorpus
{
    private const string RecordEnd = "||||END_OF_RECORD";

    /// <summary>
    /// How many spans id-phi.phrase lists in deid 1.1. A truncated copy would otherwise be
    /// scored as if its notes had less PHI.
    /// </summary>
    private const int ReleaseGoldSpans = 1779;

    public string Id => "physionet-deid";

    public string Title => "PhysioNet Deidentified Medical Text 1.0";

    public LabelMap Labels { get; } = new(
    [
        new LabelRule("PTName", PhiCategory.Name, SafeHarbor: true),
        new LabelRule("PTNameInitial", PhiCategory.Name, SafeHarbor: true),
        new LabelRule("RelativeProxyName", PhiCategory.Name, SafeHarbor: true),
        new LabelRule("HCPName", PhiCategory.Name, SafeHarbor: true),
        new LabelRule("Location", PhiCategory.Location, SafeHarbor: true),
        new LabelRule("Date", PhiCategory.Date, SafeHarbor: true),
        // A year on its own: Safe Harbor keeps it, and so does DeidSharp.
        new LabelRule("DateYear", PhiCategory.Date, SafeHarbor: false),
        new LabelRule("Phone", PhiCategory.Phone, SafeHarbor: true),
        // The gold standard marks ages over 89 only.
        new LabelRule("Age", PhiCategory.Age, SafeHarbor: true),
        new LabelRule("Other", PhiCategory.OtherIdentifier, SafeHarbor: true),
    ]);

    public IReadOnlyList<CorpusDocument> Read()
    {
        var notes = ReadNotes();
        var gold = ReadGold(notes.ToDictionary(n => n.Key, n => n.Text));
        return [.. notes.Select(n => new CorpusDocument(n.Key.ToString(), n.Text, gold.GetValueOrDefault(n.Key) ?? [], $"patient {n.Key.Patient}"))];
    }

    private List<(NoteKey Key, string Text)> ReadNotes()
    {
        var notes = new List<(NoteKey Key, string Text)>();
        var seen = new HashSet<NoteKey>();
        NoteKey? current = null;
        var text = new StringBuilder();
        var lineNumber = 0;

        // Split on "\n" only and keep any "\r", as the deid software (Perl's chomp) does.
        foreach (var line in File.ReadAllText(textPath, Encoding.Latin1).Split('\n'))
        {
            lineNumber++;
            var header = RecordStart().Match(line);
            if (header.Success)
            {
                if (current is not null)
                {
                    throw new CorpusFormatException($"id.text line {lineNumber}: a record starts before the previous one ends.");
                }

                current = new NoteKey(Number(header, "patient"), Number(header, "note"));
                if (!seen.Add(current.Value))
                {
                    throw new CorpusFormatException($"id.text line {lineNumber}: {current} appears twice.");
                }

                text.Clear();
                continue;
            }

            // The deid software ignores anything between records too.
            if (current is null)
            {
                continue;
            }

            var end = line.IndexOf(RecordEnd, StringComparison.Ordinal);
            if (end < 0)
            {
                text.Append(line).Append('\n');
                continue;
            }

            text.Append(line, 0, end);
            notes.Add((current.Value, text.ToString()));
            current = null;
        }

        if (current is not null)
        {
            throw new CorpusFormatException($"id.text: {current} has no end-of-record line.");
        }

        return notes.Count > 0 ? notes : throw new CorpusFormatException("id.text holds no records.");
    }

    private Dictionary<NoteKey, List<LabeledSpan>> ReadGold(Dictionary<NoteKey, string> notes)
    {
        var gold = new Dictionary<NoteKey, List<LabeledSpan>>();
        int lineNumber = 0, mismatches = 0, firstMismatch = 0;
        foreach (var line in File.ReadLines(goldPath, Encoding.Latin1))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var m = GoldLine().Match(line);
            if (!m.Success)
            {
                throw new CorpusFormatException($"id-phi.phrase line {lineNumber} is not \"patient note start end type text\".");
            }

            var key = new NoteKey(Number(m, "patient"), Number(m, "note"));
            var (start, end, label) = (Number(m, "start"), Number(m, "end"), m.Groups["label"].Value);
            if (!Labels.Knows(label))
            {
                throw new CorpusFormatException($"id-phi.phrase line {lineNumber}: a PHI type the corpus's label map doesn't list.");
            }

            if (!notes.TryGetValue(key, out var note))
            {
                throw new CorpusFormatException($"id-phi.phrase line {lineNumber}: {key} is not in id.text.");
            }

            if (end <= start || end > note.Length || Collapse(note.AsSpan(start, end - start)) != Collapse(m.Groups["text"].ValueSpan))
            {
                mismatches++;
                firstMismatch = firstMismatch == 0 ? lineNumber : firstMismatch;
                continue;
            }

            if (!gold.TryGetValue(key, out var spans))
            {
                gold[key] = spans = [];
            }

            spans.Add(new LabeledSpan(start, end, label));
        }

        if (mismatches > 0)
        {
            throw new CorpusFormatException(
                $"{mismatches} gold spans don't match the note text at their offsets (the first on id-phi.phrase line {firstMismatch}). " +
                "Check that id.text and id-phi.phrase come from the same release, unmodified.");
        }

        var count = gold.Values.Sum(s => s.Count);
        var expected = expectedGoldSpans ?? ReleaseGoldSpans;
        return count == expected
            ? gold
            : throw new CorpusFormatException($"id-phi.phrase holds {count:N0} gold spans, not the {expected:N0} expected. Use the unmodified file from deid 1.1.");
    }

    private static int Number(Match match, string group) => int.Parse(match.Groups[group].ValueSpan, CultureInfo.InvariantCulture);

    // The phrase file writes a span's text on one line; compare with runs of whitespace as one space.
    private static string Collapse(ReadOnlySpan<char> text) => Whitespace().Replace(text.ToString(), " ").Trim();

    [GeneratedRegex(@"\ASTART_OF_RECORD=(?<patient>\d+)\|+(?<note>\d+)\|+")]
    private static partial Regex RecordStart();

    [GeneratedRegex(@"\A(?<patient>\d+)\s+(?<note>\d+)\s+(?<start>\d+)\s+(?<end>\d+)\s+(?<label>\S+)\s+(?<text>.*?)\s*\z")]
    private static partial Regex GoldLine();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private readonly record struct NoteKey(int Patient, int Note)
    {
        public override string ToString() => $"patient {Patient}, note {Note}";
    }
}
