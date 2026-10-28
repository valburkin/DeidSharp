using System.Text;
using System.Text.RegularExpressions;

namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

/// <summary>
/// Turns a note whose PHI was replaced by <c>___</c> into a note with surrogate PHI and its
/// gold spans: each placeholder becomes what its context suggests (<see cref="PlaceholderRules"/>),
/// drawn from the note's made-up identity (<see cref="NoteSurrogates"/>), and every piece of
/// PHI put in becomes a gold span. This is how "hiding in plain sight" corpora are built
/// (Carrell et al. 2013): the gold standard comes from the original de-identification, so no
/// one annotates, and the text reads like a note with real PHI in it.
/// </summary>
internal static partial class SurrogateFiller
{
    /// <summary>What MIMIC-IV-Note put in place of each piece of PHI.</summary>
    public const string Placeholder = "___";

    public static (string Text, IReadOnlyList<LabeledSpan> Spans) Fill(string note, NoteSurrogates surrogates)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(surrogates);
        var text = new StringBuilder(note.Length + 1_024);
        var spans = new List<LabeledSpan>();
        var copied = 0;
        foreach (Match placeholder in PlaceholderPattern().Matches(note))
        {
            text.Append(note, copied, placeholder.Index - copied);
            copied = placeholder.Index + placeholder.Length;

            // The rules read the note as it came, other placeholders included ("from ___ to ___").
            var before = note[Math.Max(0, placeholder.Index - PlaceholderRules.ContextLength)..placeholder.Index];
            var after = note[copied..Math.Min(note.Length, copied + PlaceholderRules.ContextLength)];
            var rule = PlaceholderRules.Match(before, after);
            foreach (var piece in surrogates.Render(rule.Kind))
            {
                var start = text.Length;
                text.Append(piece.Text);
                if (piece.Label is { } label)
                {
                    spans.Add(new LabeledSpan(start, text.Length, label, rule.Name));
                }
            }
        }

        text.Append(note, copied, note.Length - copied);
        return (text.ToString(), spans);
    }

    /// <summary>The note's sex field ("Sex:   F"), so the patient's first name fits it; null if it has none.</summary>
    public static char? Sex(string note)
    {
        var match = SexField().Match(note);
        return match.Success ? char.ToUpperInvariant(match.Groups["sex"].Value[0]) : null;
    }

    // Exactly three: a longer run of underscores is a line drawn in a form, not removed PHI.
    [GeneratedRegex("(?<!_)___(?!_)")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\bSex:[ \t]*(?<sex>[FM])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SexField();
}
