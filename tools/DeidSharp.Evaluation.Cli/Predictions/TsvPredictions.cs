using System.Globalization;

namespace DeidSharp.Evaluation.Cli.Predictions;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the reader for the tool's own format, the one any tool's output can be converted to.

/// <summary>
/// One span per line, tab-separated: <c>document</c>, <c>start</c>, <c>end</c> and, optionally,
/// a DeidSharp category (<c>Name</c>; <c>OtherIdentifier</c> if left out). Blank lines and
/// lines starting with <c>#</c> are skipped.
/// </summary>
internal sealed class TsvPredictions : IPredictionsFormat
{
    public IEnumerable<PredictedSpan> Read(string name, string path)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length is < 3 or > 4
                || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var end))
            {
                throw new UsageException($"--predictions {name}: line {lineNumber} is not \"document<TAB>start<TAB>end[<TAB>category]\".");
            }

            var category = PhiCategory.OtherIdentifier;

            // Names only: Enum.TryParse would also take "3" or "Name, Date".
            if (fields.Length == 4 && (!fields[3].All(char.IsAsciiLetter) || !Enum.TryParse(fields[3], ignoreCase: false, out category)))
            {
                throw new UsageException($"--predictions {name}: line {lineNumber} has a category DeidSharp doesn't know.");
            }

            yield return new PredictedSpan(lineNumber, fields[0], start, end, category);
        }
    }
}
