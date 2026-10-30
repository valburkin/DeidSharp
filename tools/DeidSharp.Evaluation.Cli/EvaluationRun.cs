using System.Diagnostics;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Predictions;
using DeidSharp.Evaluation.Cli.Reporting;
using DeidSharp.Evaluation.Cli.Scoring;

namespace DeidSharp.Evaluation.Cli;

/// <summary>
/// One run: reads the corpus, scores each stack, reports the numbers and the miss profile,
/// then writes the misses file and publishes the results if the settings ask for them.
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

        if (settings.Output is not null)
        {
            await settings.Output.WriteAsync(documents, output);
            return;
        }

        if (settings.MissProfileLabel is not null)
        {
            MissProfile.EnsureLabel(settings.MissProfileLabel, documents);
        }

        var files = settings.Predictions.Select(p => PredictionsFile.Read(p.Name, p.Path, corpus)).ToList();
        using var stacks = new DetectorStacks(settings.Presidio, settings.Clinical, settings.DroppedSources, settings.Variants, settings.LlmModels, settings.LlmEndpoint, settings.LlmPrompts, settings.Gazetteer, settings.SecondOpinions, settings.Ablation, settings.ClinicalPlaces);
        if (settings.PublishDirectory is not null
            && HeadlineStacks.MissingFrom(
                stacks.Stacks.Select(s => s.Name),
                settings.LlmModels.Count > 0,
                ResultsPublisher.Load(settings.PublishDirectory).Find(settings.Corpus.Id, EvaluationScopes.Id(settings.Scope), CorpusSplits.Id(settings.Split))) is { Count: > 0 } missing)
        {
            throw new UsageException(
                $"--publish replaces the corpus's published results, so the run must measure the headline stacks, the README's and those already published; this one wouldn't measure: {string.Join(", ", missing)}.");
        }

        await stacks.EnsureReachableAsync(cancellationToken);

        // The weights a published number came from, read before the run and checked after it.
        var models = settings.PublishDirectory is null ? null : await stacks.PinModelsAsync(cancellationToken);

        var bootstrap = new Bootstrap(documents.Select(d => d.SplitGroup).Distinct(StringComparer.Ordinal).Count());
        var results = new List<StackResult>();
        // The sensitivity check wraps every stack and tool alike, so their comparisons stay paired.
        var scoredStacks = stacks.Stacks.Concat<IScoredStack>(files)
            .Select(s => settings.OutOfScopeNeutral ? new OutOfScopeNeutral(s, settings.Corpus.Labels, settings.Scope) : s);
        foreach (var stack in scoredStacks)
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
        if (settings.OutOfScopeNeutral)
        {
            await output.WriteLineAsync();
            await output.WriteLineAsync("Left out the predictions that touch only gold outside the scope (--out-of-scope neutral).");
        }

        if (settings.DroppedSources.Count > 0)
        {
            await output.WriteLineAsync();
            await output.WriteLineAsync($"Left out the spans only these reported: {string.Join(", ", settings.DroppedSources.Order(StringComparer.Ordinal))}.");
        }

        if (settings.MissProfileLabel is not null)
        {
            MissProfile.Write(output, settings.MissProfileLabel, settings.Corpus, settings.Scope, documents, results);
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
            // A pull or an update during a run of hours would publish numbers under weights they didn't come from.
            models?.EnsureUnchanged(await stacks.PinModelsAsync(cancellationToken));

            var notice = ResultsPublisher.Publish(
                settings.PublishDirectory,
                CorpusResult.From(settings.Corpus, settings.Scope, settings.Split, documents.Count, results, BuildVersion.OfLibrary(), today, settings.Note, models));
            await output.WriteLineAsync();
            await output.WriteLineAsync($"Published to {Path.GetFullPath(settings.PublishDirectory)}.");
            if (notice is not null)
            {
                await output.WriteLineAsync(notice);
            }
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
