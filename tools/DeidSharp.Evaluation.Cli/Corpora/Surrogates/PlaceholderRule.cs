using System.Text.RegularExpressions;

namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

/// <summary>
/// One way to tell what a placeholder stood for: a pattern the text just before it must end
/// with, a pattern the text just after it must start with, or both. A rule with neither
/// matches every placeholder.
/// </summary>
/// <param name="name">For tests and diagnostics ("doctor title").</param>
/// <param name="before">What the text before must end with, case-insensitive; null for anything.</param>
/// <param name="after">What the text after must start with, case-insensitive; null for anything.</param>
internal sealed class PlaceholderRule(string name, string? before, string? after, SurrogateKind kind)
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // The contexts are short, so a pattern that takes longer than this is broken.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private readonly Regex? _before = before is null ? null : new Regex("(?:" + before + @")\z", Options, Timeout);
    private readonly Regex? _after = after is null ? null : new Regex(@"\A(?:" + after + ")", Options, Timeout);

    public string Name => name;

    public SurrogateKind Kind => kind;

    public bool Matches(string textBefore, string textAfter) =>
        (_before?.IsMatch(textBefore) ?? true) && (_after?.IsMatch(textAfter) ?? true);
}
