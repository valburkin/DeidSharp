using System.Globalization;
using System.Text.RegularExpressions;

namespace DeidSharp.Evaluation.Cli.Predictions;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the reader for the .phi file PhysioNet's deid software writes, so its output is
// scored as it comes, with no conversion step to get wrong.

/// <summary>
/// The PHI locations PhysioNet's deid software (version 1.1; GNU GPL or Library GPL, version 2 or later; run
/// separately, never part of this repository) writes to <c>&lt;input&gt;.phi</c>:
/// a <c>Patient P&lt;TAB&gt;Note N</c> line for each note, then a <c>start&lt;TAB&gt;start&lt;TAB&gt;end</c>
/// line for each span, the end exclusive, counted in the text deid rebuilds from the note, as
/// PhysioNet's corpus reader does. The file holds positions only; deid's other outputs
/// (<c>.res</c>, <c>.info</c>) hold note text. deid gives no category, so every span is
/// <see cref="PhiCategory.OtherIdentifier"/>.
/// </summary>
internal sealed partial class DeidPhiPredictions : IPredictionsFormat
{
    public IEnumerable<PredictedSpan> Read(string name, string path)
    {
        string? document = null;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var note = NoteLine().Match(line);
            if (note.Success)
            {
                // The id PhysioNet's corpus reader gives the note.
                document = $"patient {Number(note, "patient")}, note {Number(note, "note")}";
                continue;
            }

            var span = SpanLine().Match(line);
            if (!span.Success || document is null || span.Groups["start"].Value != span.Groups["again"].Value)
            {
                throw new UsageException($"--predictions {name}: line {lineNumber} is not deid's \"Patient P<TAB>Note N\" or \"start<TAB>start<TAB>end\".");
            }

            yield return new PredictedSpan(lineNumber, document, Number(span, "start"), Number(span, "end"), PhiCategory.OtherIdentifier);
        }
    }

    private static int Number(Match match, string group) => int.Parse(match.Groups[group].ValueSpan, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\APatient (?<patient>\d{1,9})\tNote (?<note>\d{1,9})\r?\z")]
    private static partial Regex NoteLine();

    [GeneratedRegex(@"\A(?<start>\d{1,9})\t(?<again>\d{1,9})\t(?<end>\d{1,9})\r?\z")]
    private static partial Regex SpanLine();
}
