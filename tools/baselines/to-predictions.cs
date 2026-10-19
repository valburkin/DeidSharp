// Converts another tool's output on a corpus written by the evaluation tool's --export into
// the tool's own predictions format, one span per line (document<TAB>start<TAB>end), for
// --predictions. The output holds positions only.
//
//   dotnet run tools/baselines/to-predictions.cs -- deid <export folder> <deid.phi> <spans.tsv>
//   dotnet run tools/baselines/to-predictions.cs -- philter <export folder> <philter output folder> <spans.tsv>
//
// deid writes the positions of what it found to <input>.phi, counted in bytes of each record's
// text. Philter (philter-ucsf, run with --outputformat asterisk) writes each note with every
// letter and digit it removes replaced by "*", and each run of those becomes a span. Its XML
// output lists only what its blacklists matched, not everything it removes, so it isn't used.
// Offsets come out as UTF-16 code units, end exclusive, in the text the corpus reader builds.
// Messages name files and line numbers, never text: the folders hold corpus text.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length != 4 || args[0] is not ("deid" or "philter"))
{
    Console.Error.WriteLine("Usage: dotnet run tools/baselines/to-predictions.cs -- deid|philter <export folder> <tool output> <spans.tsv>");
    return 2;
}

var (tool, export, input, outputPath) = (args[0], args[1], args[2], args[3]);
var documents = File.ReadLines(Path.Combine(export, "documents.tsv"))
    .Where(line => line.Length > 0 && !line.StartsWith('#'))
    .Select(line => line.Split('\t'))
    .Select(f => (Key: f[0], Id: f[1]))
    .ToList();

// The exported text, as the corpus reader built it (File.ReadAllText drops the byte order mark).
string Text(string key) => File.ReadAllText(Path.Combine(export, "text", key + ".txt"), Encoding.UTF8);

var spans = tool == "deid" ? FromDeid() : FromPhilter();
var tsv = new StringBuilder($"# {tool} spans, converted by tools/baselines/to-predictions.cs\n");
foreach (var (id, start, end) in spans)
{
    tsv.Append(CultureInfo.InvariantCulture, $"{id}\t{start}\t{end}\n");
}

File.WriteAllText(outputPath, tsv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"Wrote {spans.Count.ToString("N0", CultureInfo.InvariantCulture)} spans for {documents.Count.ToString("N0", CultureInfo.InvariantCulture)} documents.");
return 0;

// deid's .phi: a "Patient P<TAB>Note N" line per record, then "start<TAB>start<TAB>end" per span, in bytes.
List<(string Id, int Start, int End)> FromDeid()
{
    var byNumber = documents.ToDictionary(d => int.Parse(d.Key, CultureInfo.InvariantCulture));
    var note = new Regex(@"\APatient (\d+)\tNote 1\r?\z", RegexOptions.CultureInvariant);
    var span = new Regex(@"\A(\d+)\t\1\t(\d+)\r?\z", RegexOptions.CultureInvariant);
    var result = new List<(string, int, int)>();
    (string Id, int[] Map, int Length)? current = null;
    var lineNumber = 0;
    foreach (var line in File.ReadLines(input))
    {
        lineNumber++;
        if (line.Length == 0)
        {
            continue;
        }

        if (note.Match(line) is { Success: true } n)
        {
            var document = byNumber.TryGetValue(int.Parse(n.Groups[1].Value, CultureInfo.InvariantCulture), out var d)
                ? d
                : throw new InvalidDataException($"{input} line {lineNumber} names a record the export doesn't have.");
            var text = Text(document.Key);

            // deid's record text ends with the newline before the end-of-record marker.
            current = (document.Id, ByteToChar(text + "\n"), text.Length);
            continue;
        }

        if (span.Match(line) is not { Success: true } s || current is not { } c)
        {
            throw new InvalidDataException($"{input} line {lineNumber} is not deid's \"Patient P<TAB>Note 1\" or \"start<TAB>start<TAB>end\".");
        }

        var (startByte, endByte) = (int.Parse(s.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(s.Groups[2].Value, CultureInfo.InvariantCulture));
        if (endByte >= c.Map.Length || startByte >= endByte)
        {
            throw new InvalidDataException($"{input} line {lineNumber} has a span that doesn't fit its record.");
        }

        var (start, end) = (c.Map[startByte], Math.Min(c.Map[endByte], c.Length));
        if (start < end)
        {
            result.Add((c.Id, start, end));
        }
    }

    return result;
}

// For each byte offset of the text in UTF-8, its UTF-16 offset. deid's offsets fall between
// characters; a byte inside a character maps to that character's end.
static int[] ByteToChar(string text)
{
    var map = new List<int>();
    var utf16 = 0;
    foreach (var rune in text.EnumerateRunes())
    {
        for (var b = 0; b < rune.Utf8SequenceLength; b++)
        {
            map.Add(b == 0 ? utf16 : utf16 + rune.Utf16SequenceLength);
        }

        utf16 += rune.Utf16SequenceLength;
    }

    map.Add(utf16);
    return [.. map];
}

// Philter's asterisk output: the note, character for character, with what it removed as "*".
List<(string Id, int Start, int End)> FromPhilter()
{
    var result = new List<(string, int, int)>();
    foreach (var (key, id) in documents)
    {
        var text = Text(key);
        var outputFile = Path.Combine(input, key + ".txt");
        if (!File.Exists(outputFile))
        {
            throw new InvalidDataException($"Philter wrote no output for document {key}.");
        }

        // Python reads "\r" as a line end, which would shift every offset after it.
        if (text.Contains('\r', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Document {key} holds a carriage return, which Philter's offsets would not count.");
        }

        var original = text.EnumerateRunes().ToList();
        var redacted = File.ReadAllText(outputFile, Encoding.UTF8).EnumerateRunes().ToList();
        if (original.Count != redacted.Count)
        {
            throw new InvalidDataException($"Philter's output for document {key} has another length than its input.");
        }

        int utf16 = 0, start = -1;
        for (var i = 0; i <= original.Count; i++)
        {
            var removed = i < original.Count && redacted[i].Value == '*' && original[i].Value != '*';
            if (removed && start < 0)
            {
                start = utf16;
            }
            else if (!removed && start >= 0)
            {
                result.Add((id, start, utf16));
                start = -1;
            }

            if (i < original.Count)
            {
                utf16 += original[i].Utf16SequenceLength;
            }
        }
    }

    return result;
}
