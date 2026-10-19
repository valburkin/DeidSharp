using System.Diagnostics;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Predictions;
using DeidSharp.Evaluation.Cli.Reporting;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli;

/// <summary>
/// One run: reads the corpus, scores each stack, reports the numbers, then writes the
/// misses file and publishes the results if the settings ask for them.
/// </summary>
internal static class EvaluationRun
{
    public static async Task RunAsync(EvaluationSettings settings, TextWriter output, TextWriter status, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(status);

        var corpus = settings.Corpus.Read();
        var documents = corpus.Where(d => CorpusSplits.Includes(settings.Split, d.SplitGroup)).ToList();
        if (documents.Count == 0)
        {
            throw new UsageException("The corpus has no documents in this split.");
        }

        if (settings.ExportDirectory is not null)
        {
            CorpusExport.Write(settings.ExportDirectory, documents);
            await output.WriteLineAsync(Formats.Invariant($"Wrote {documents.Count:N0} documents to {Path.GetFullPath(settings.ExportDirectory)}. They hold corpus text: keep the folder out of Git."));
            return;
        }

        var files = settings.Predictions.Select(p => PredictionsFile.Read(p.Name, p.Path, corpus)).ToList();
        using var stacks = new DetectorStacks(settings.Presidio, settings.Clinical, settings.DroppedSources, settings.Variants, settings.LlmModels, settings.LlmEndpoint, settings.LlmPrompts, settings.Gazetteer, settings.SecondOpinions, settings.Ablation, settings.ClinicalPlaces);
        await stacks.EnsureReachableAsync(cancellationToken);

        var bootstrap = new Bootstrap(documents.Select(d => d.SplitGroup).Distinct(StringComparer.Ordinal).Count());
        var results = new List<StackResult>();
        foreach (var stack in stacks.Stacks.Concat<IScoredStack>(files))
        {
            await status.WriteLineAsync(Formats.Invariant($"Scoring {stack.Name} on {documents.Count:N0} documents"));
            var started = Stopwatch.GetTimestamp();
            var result = await StackScorer.ScoreAsync(stack, settings.Corpus, documents, settings.Scope, new StatusProgress(status, documents.Count), cancellationToken);

            // A model's time shows on the first stack that asks it; later stacks reuse its answers.
            var elapsed = Stopwatch.GetElapsedTime(started);
            await status.WriteLineAsync(Formats.Invariant($"  done in {elapsed.TotalSeconds:N0} s, {elapsed.TotalMilliseconds / documents.Count:N0} ms per document"));
            results.Add(result with { Intervals = bootstrap.Intervals(result) });
        }

        // Each variant against the stack it wraps, on the same resamples.
        var scored = results.ToDictionary(r => r.Stack, StringComparer.Ordinal);
        results = [.. results.Select(r => r.Baseline is { } name && scored.TryGetValue(name, out var baseline)
            ? r with { Gain = bootstrap.Gain(baseline, r) }
            : r)];

        // Every stack against each tool named, on the same resamples: a paired test of the difference.
        List<Comparison> comparisons = [.. settings.CompareWith.SelectMany(tool => results
            .Where(r => r.Stack != tool)
            .Select(r => new Comparison(tool, r.Stack, bootstrap.Gain(scored[tool], r))))];

        ConsoleReport.Write(output, settings.Corpus, settings.Scope, settings.Split, documents.Count, results, comparisons);
        if (settings.DroppedSources.Count > 0)
        {
            await output.WriteLineAsync();
            await output.WriteLineAsync($"Left out the spans only these reported: {string.Join(", ", settings.DroppedSources.Order(StringComparer.Ordinal))}.");
        }

        if (settings.MissesPath is not null)
        {
            MissesFile.Write(settings.MissesPath, results);
            await output.WriteLineAsync();
            await output.WriteLineAsync($"Missed spans, with their text, are in {Path.GetFullPath(settings.MissesPath)}. Keep that file out of Git.");
        }

        if (settings.FalsePositivesPath is not null)
        {
            FalsePositivesFile.Write(settings.FalsePositivesPath, results);
            await output.WriteLineAsync();
            await output.WriteLineAsync($"False positives, with their text, are in {Path.GetFullPath(settings.FalsePositivesPath)}. Keep that file out of Git.");
        }

        if (settings.PublishDirectory is not null)
        {
            ResultsPublisher.Publish(
                settings.PublishDirectory,
                CorpusResult.From(settings.Corpus, settings.Scope, settings.Split, documents.Count, results, BuildVersion.OfLibrary(), today, settings.Note));
            await output.WriteLineAsync();
            await output.WriteLineAsync($"Published to {Path.GetFullPath(settings.PublishDirectory)}.");
        }
    }

    // Reports synchronously (Progress<T> posts to the thread pool, which scrambles console order).
    private sealed class StatusProgress(TextWriter status, int total) : IProgress<int>
    {
        public void Report(int value)
        {
            if (value % 250 == 0 || value == total)
            {
                status.WriteLine(Formats.Invariant($"  {value:N0} / {total:N0}"));
            }
        }
    }
}
