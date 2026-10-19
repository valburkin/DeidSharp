using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>The run's numbers on the console: counts and rates, never text from the corpus.</summary>
internal static class ConsoleReport
{
    private const int TopSources = 20;

    public static void Write(TextWriter output, ICorpus corpus, EvaluationScope scope, CorpusSplit split, int documents, IReadOnlyList<StackResult> results, IReadOnlyList<Comparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(comparisons);
        if (results.Count == 0)
        {
            return;
        }

        var width = Math.Max("Stack".Length, results.Max(r => r.Stack.Length));
        output.WriteLine();
        output.WriteLine(corpus.Title);
        output.WriteLine(Formats.Invariant($"{documents:N0} documents · {results[0].Report.Overall.GoldSpans:N0} gold spans · scope {EvaluationScopes.Id(scope)} · split {CorpusSplits.Id(split)}"));
        output.WriteLine();
        output.WriteLine($"{"Stack".PadRight(width)}  {"Recall",8}  {"Char recall",11}  {"Precision",9}  {"Missed",7}");
        foreach (var r in results)
        {
            output.WriteLine(
                $"{r.Stack.PadRight(width)}  {Formats.Percent(r.Report.Overall.Recall),8}  {Formats.Percent(r.Report.CharacterRecall),11}  {Formats.Percent(r.Report.Overall.Precision),9}  {Formats.Invariant($"{r.Misses.Count:N0}"),7}");
        }

        WriteComparable(output, results, width);
        WriteIntervals(output, results, width);
        WriteGains(output, results, width);
        WriteComparisons(output, comparisons, width);

        var labels = results[0].Labels;
        var labelWidth = Math.Max("Label".Length, labels.Count > 0 ? labels.Max(l => l.Label.Length) : 0);
        output.WriteLine();
        output.WriteLine("Recall by corpus label");
        output.WriteLine($"{"Label".PadRight(labelWidth)}  {"Gold",6}  " + string.Join("  ", results.Select(r => r.Stack.PadLeft(Column(r)))));
        foreach (var label in labels)
        {
            var cells = results.Select(r => Formats.Percent(r.Labels.FirstOrDefault(l => l.Label == label.Label)?.Recall).PadLeft(Column(r)));
            output.WriteLine($"{label.Label.PadRight(labelWidth)}  {Formats.Invariant($"{label.GoldSpans:N0}"),6}  " + string.Join("  ", cells));
        }

        var categories = results.SelectMany(r => r.FalsePositivesByCategory.Select(c => c.Category)).Distinct().ToList();
        if (categories.Count == 0)
        {
            return;
        }

        var categoryWidth = Math.Max("Category".Length, categories.Max(c => c.ToString().Length));
        output.WriteLine();
        output.WriteLine("False positives by predicted category");
        output.WriteLine($"{"Category".PadRight(categoryWidth)}  " + string.Join("  ", results.Select(r => r.Stack.PadLeft(Column(r)))));
        foreach (var category in categories.OrderByDescending(c => results.Max(r => Count(r, c))))
        {
            var cells = results.Select(r => Formats.Invariant($"{Count(r, category):N0}").PadLeft(Column(r)));
            output.WriteLine($"{category.ToString().PadRight(categoryWidth)}  " + string.Join("  ", cells));
        }

        WriteSources(output, results);
    }

    // The units other papers report in: words PHI or not (Philter, pyclipse), and the clinical text lost.
    private static void WriteComparable(TextWriter output, IReadOnlyList<StackResult> results, int width)
    {
        output.WriteLine();
        output.WriteLine("Per word (PHI or not) and non-PHI text removed");
        output.WriteLine($"{"Stack".PadRight(width)}  {"Word recall",11}  {"Word precision",14}  {"Word F1",8}  {"Non-PHI removed",15}");
        foreach (var r in results)
        {
            var words = r.Report.Words;
            output.WriteLine(
                $"{r.Stack.PadRight(width)}  {Formats.Percent(words.Recall),11}  {Formats.Percent(words.Precision),14}  {Formats.Percent(words.F1),8}  {Formats.Percent(r.Report.NonPhiRemoved),15}");
        }
    }

    private static void WriteIntervals(TextWriter output, IReadOnlyList<StackResult> results, int width)
    {
        if (results.Any(r => r.Intervals is null))
        {
            return;
        }

        const int cell = 13;
        output.WriteLine();
        output.WriteLine(Formats.Invariant($"95% confidence intervals (bootstrap by patient, or by document in a corpus without patients; {Bootstrap.DefaultResamples:N0} resamples)"));
        output.WriteLine($"{"Stack".PadRight(width)}  {"Recall",cell}  {"Char recall",cell}  {"Precision",cell}  {"Word F1",cell}  {"Non-PHI removed",15}");
        foreach (var r in results)
        {
            var i = r.Intervals!;
            output.WriteLine(
                $"{r.Stack.PadRight(width)}  {Formats.Range(i.Recall.Low, i.Recall.High),cell}  {Formats.Range(i.CharacterRecall.Low, i.CharacterRecall.High),cell}  " +
                $"{Formats.Range(i.Precision.Low, i.Precision.High),cell}  {Formats.Range(i.WordF1.Low, i.WordF1.High),cell}  {Formats.Range(i.NonPhiRemoved.Low, i.NonPhiRemoved.High),15}");
        }
    }

    private static void WriteGains(TextWriter output, IReadOnlyList<StackResult> results, int width)
    {
        var variants = results.Where(r => r.Gain is not null).ToList();
        if (variants.Count == 0)
        {
            return;
        }

        const int cell = 26;
        output.WriteLine();
        output.WriteLine("Change from the stack each variant wraps, in points (paired bootstrap, 95% CI)");
        output.WriteLine($"{"Stack".PadRight(width)}  {"Recall",cell}  {"Precision",cell}  {"Word F1",cell}");
        foreach (var r in variants)
        {
            var g = r.Gain!;
            output.WriteLine($"{r.Stack.PadRight(width)}  {Points(g.Recall),cell}  {Points(g.Precision),cell}  {Points(g.WordF1),cell}");
        }
    }

    private static void WriteComparisons(TextWriter output, IReadOnlyList<Comparison> comparisons, int width)
    {
        const int cell = 26;
        foreach (var tool in comparisons.GroupBy(c => c.Tool, StringComparer.Ordinal))
        {
            output.WriteLine();
            output.WriteLine($"Change from {tool.Key}, in points (paired bootstrap, 95% CI)");
            output.WriteLine($"{"Stack".PadRight(width)}  {"Recall",cell}  {"Precision",cell}  {"Word F1",cell}");
            foreach (var c in tool)
            {
                output.WriteLine($"{c.Stack.PadRight(width)}  {Points(c.Gain.Recall),cell}  {Points(c.Gain.Precision),cell}  {Points(c.Gain.WordF1),cell}");
            }
        }
    }

    private static string Points(Change change) => Formats.Invariant(
        $"{change.Value * 100:+0.00;-0.00;0.00} ({change.Interval.Low * 100:+0.00;-0.00;0.00} to {change.Interval.High * 100:+0.00;-0.00;0.00})");

    private static void WriteSources(TextWriter output, IReadOnlyList<StackResult> results)
    {
        var sources = results.SelectMany(r => r.FalsePositivesBySource.Select(s => s.Source)).Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => results.Max(r => Count(r, s))).ThenBy(s => s, StringComparer.Ordinal).Take(TopSources).ToList();
        if (sources.Count == 0)
        {
            return;
        }

        var width = Math.Max("Source".Length, sources.Max(s => s.Length));
        output.WriteLine();
        output.WriteLine($"False positives by source (top {TopSources})");
        output.WriteLine($"{"Source".PadRight(width)}  " + string.Join("  ", results.Select(r => r.Stack.PadLeft(Column(r)))));
        foreach (var source in sources)
        {
            output.WriteLine($"{source.PadRight(width)}  " + string.Join("  ", results.Select(r => Formats.Invariant($"{Count(r, source):N0}").PadLeft(Column(r)))));
        }
    }

    private static int Count(StackResult result, string source) =>
        result.FalsePositivesBySource.FirstOrDefault(c => c.Source == source).Count;

    private static int Count(StackResult result, PhiCategory category) =>
        result.FalsePositivesByCategory.FirstOrDefault(c => c.Category == category).Count;

    private static int Column(StackResult result) => Math.Max(8, result.Stack.Length);
}
