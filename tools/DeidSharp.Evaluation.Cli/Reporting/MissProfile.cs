using System.Collections.Frozen;
using System.Text.RegularExpressions;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Reporting;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each feature a missed span may have is one row, its name and its test; the
// profile counts every row the same way, so a new question is a new row.

/// <summary>
/// Why one label's gold spans were missed, in counts only: for each stack, how many of the
/// spans with each feature it missed (one word, an ordinary English word, a name the note
/// mentions again and the stack found there). The features describe the span's text, but
/// the report shows only how many spans have each, so it can run on a restricted corpus.
/// A span can have several features, so the rows overlap and don't add up.
/// </summary>
internal static class MissProfile
{
    public const string Option = "miss-profile";

    // The gazetteer's list of common English words, which the build copies next to the tool.
    private static readonly Lazy<FrozenSet<string>> CommonWords = new(() =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "data", "common-words.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToFrozenSet(StringComparer.Ordinal));

    private static readonly (string Name, Func<ProfiledSpan, Func<LabeledSpan, bool>, bool> Has)[] Features =
    [
        ("every span", (_, _) => true),
        ("one word", (s, _) => s.Words == 1),
        ("several words", (s, _) => s.Words > 1),
        ("an ordinary English word", (s, _) => s.Words == 1 && CommonWords.Value.Contains(s.Text.ToLowerInvariant())),
        ("a lowercase word elsewhere in the note", (s, _) => s.LowercaseElsewhere),
        ("a word in another label's span", (s, _) => s.InsideOtherLabel),
        ("in capitals", (s, _) => s.Text.Any(char.IsLetter) && !s.Text.Any(char.IsLower)),
        ("capitalized", (s, _) => char.IsUpper(s.Text[0]) && s.Text.Any(char.IsLower)),
        ("lowercase", (s, _) => s.Text.Any(char.IsLetter) && !s.Text.Any(char.IsUpper)),
        ("no other gold span with its text", (s, _) => s.OtherMentions.Count == 0),
        ("another with its text, found", (s, found) => s.OtherMentions.Any(found)),
        ("others with its text, none found", (s, found) => s.OtherMentions.Count > 0 && !s.OtherMentions.Any(found)),
    ];

    /// <summary>
    /// Refuses a label no gold span in these documents has, and reads the word list, before any
    /// stack runs: a missing file stops the run at once, not after hours of scoring.
    /// </summary>
    public static void EnsureLabel(string label, IReadOnlyList<CorpusDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        _ = CommonWords.Value;
        var labels = documents.SelectMany(d => d.Spans).Select(s => s.Label).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (!labels.Contains(label, StringComparer.Ordinal))
        {
            throw new UsageException($"--{Option} takes one of this corpus's labels: {string.Join(", ", labels)}.");
        }
    }

    public static void Write(TextWriter output, string label, ICorpus corpus, EvaluationScope scope, IReadOnlyList<CorpusDocument> documents, IReadOnlyList<StackResult> results)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(results);
        if (!corpus.Labels.Counts(label, scope))
        {
            output.WriteLine();
            output.WriteLine($"No {label} span counts in this scope, so there is no profile of its misses.");
            return;
        }

        var spans = documents.SelectMany(d => Profile(d, label, corpus, scope)).ToList();
        var missed = results.Select(r => r.Misses.Select(m => (m.Document.Id, m.Span)).ToHashSet()).ToList();
        var cells = results.Select((_, i) => Features.Select(f =>
        {
            var having = spans.Where(s => f.Has(s, other => !missed[i].Contains((s.Document.Id, other)))).ToList();
            var misses = having.Count(s => missed[i].Contains((s.Document.Id, s.Span)));
            return having.Count == 0
                ? "–"
                : Formats.Invariant($"{misses:N0} / {having.Count:N0} ({Formats.Percent((double)misses / having.Count)})");
        }).ToList()).ToList();

        var featureWidth = Features.Max(f => f.Name.Length);
        var widths = results.Select((r, i) => Math.Max(r.Stack.Length, cells[i].Max(c => c.Length))).ToList();
        output.WriteLine();
        output.WriteLine($"Misses of {label} by feature (missed / spans with the feature)");
        output.WriteLine($"{"Feature".PadRight(featureWidth)}  " + string.Join("  ", results.Select((r, i) => r.Stack.PadLeft(widths[i]))));
        foreach (var (feature, row) in Features.Select((f, row) => (f, row)))
        {
            output.WriteLine($"{feature.Name.PadRight(featureWidth)}  " + string.Join("  ", cells.Select((c, i) => c[row].PadLeft(widths[i]))));
        }
    }

    // Each gold span of the label that counts, with what the features ask about it.
    private static IEnumerable<ProfiledSpan> Profile(CorpusDocument document, string label, ICorpus corpus, EvaluationScope scope)
    {
        var counted = document.Spans.Where(s => corpus.Labels.Counts(s.Label, scope)).ToList();
        foreach (var span in counted.Where(s => s.Label == label))
        {
            var text = document.Text[span.Start..span.End];
            if (text.Length == 0)
            {
                continue;
            }

            var word = new Regex($@"(?<!\w){Regex.Escape(text)}(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            yield return new ProfiledSpan(
                document,
                span,
                text,
                text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
                [.. counted.Where(o => o != span && string.Equals(document.Text[o.Start..o.End], text, StringComparison.OrdinalIgnoreCase))],
                counted.Any(o => o.Label != label && o.End - o.Start > text.Length && word.IsMatch(document.Text[o.Start..o.End])),
                word.Matches(document.Text).Any(m => m.Value.All(c => !char.IsUpper(c)) && m.Value.Any(char.IsLower)
                    && !document.Spans.Any(o => o.Start < m.Index + m.Length && m.Index < o.End)));
        }
    }

    /// <summary>A gold span with the facts the features read: its text, words and other mentions in the note.</summary>
    /// <param name="OtherMentions">The other gold spans of the note that count, of any label, with the same text ignoring case.</param>
    /// <param name="InsideOtherLabel">Whether its text is a whole word inside a longer gold span of another label anywhere in the note (a clinician's full name).</param>
    /// <param name="LowercaseElsewhere">Whether the note also uses its text in lowercase outside every gold span, as an ordinary word.</param>
    private sealed record ProfiledSpan(
        CorpusDocument Document,
        LabeledSpan Span,
        string Text,
        int Words,
        IReadOnlyList<LabeledSpan> OtherMentions,
        bool InsideOtherLabel,
        bool LowercaseElsewhere);
}
