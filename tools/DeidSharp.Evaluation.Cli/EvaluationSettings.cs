using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Parameter Object.
// The pattern: settings that travel together are grouped into one object, instead of
// a long list of separate arguments.
// Here: everything a run needs, parsed and checked from the command line before any
// corpus is read or any detector called.

/// <summary>What one run measures and where its output goes.</summary>
/// <param name="Presidio">A stock Presidio analyzer to stack with the deterministic detectors, if any.</param>
/// <param name="Clinical">The clinical sidecar to stack with the deterministic detectors, if any.</param>
/// <param name="MissesPath">Where to write the missed spans with their text, outside any Git working tree.</param>
/// <param name="FalsePositivesPath">Where to write the false positives with their text, outside any Git working tree.</param>
/// <param name="PublishDirectory">The docs/evaluation directory to update with the results, if any.</param>
/// <param name="Split">Which half of the corpus to read: tune on one, report on the other.</param>
/// <param name="Note">Published with the results: what produced them that the tool can't see (the sidecar images, CPU or GPU).</param>
/// <param name="DroppedSources">Recognizers whose spans the services' answers leave out ("clinical:SpacyRecognizer"), to see what they add.</param>
/// <param name="Predictions">Other tools' predictions to score as stacks of their own: a name and a file each.</param>
/// <param name="Variants">Changes to measure each stack with as well (<see cref="StackVariants"/>).</param>
/// <param name="LlmModels">Ollama models to add to each stack as an LLM layer, if any.</param>
/// <param name="LlmEndpoint">The Ollama server those models run on.</param>
/// <param name="LlmPrompts">The prompts each model is measured with (<see cref="LlmPrompts"/>).</param>
/// <param name="Gazetteer">Whether the stacks' deterministic layer has the gazetteer: with, without, or both families.</param>
/// <param name="ClinicalPlaces">Also measure the clinical sidecar keeping only its locations and organizations.</param>
/// <param name="Ablation">Also measure each service and model without the deterministic layer.</param>
/// <param name="SecondOpinions">The second opinions (<see cref="SecondOpinions"/>) each LLM stack is also measured with.</param>
/// <param name="CompareWith">Prediction files every stack is also compared with, paired: the names given to <paramref name="Predictions"/>.</param>
/// <param name="Output">Where to write the corpus instead of measuring anything (<see cref="CorpusOutputs"/>), if anywhere.</param>
/// <param name="MissProfileLabel">The corpus label whose misses to count by feature (<see cref="MissProfile"/>), if any.</param>
internal sealed record EvaluationSettings(
    ICorpus Corpus,
    EvaluationScope Scope,
    CorpusSplit Split,
    Uri? Presidio,
    Uri? Clinical,
    string? MissesPath,
    string? FalsePositivesPath,
    string? PublishDirectory,
    string? Note,
    IReadOnlySet<string> DroppedSources,
    IReadOnlyList<(string Name, string Path)> Predictions,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> LlmModels,
    Uri LlmEndpoint,
    IReadOnlyList<string> LlmPrompts,
    IReadOnlyList<bool> Gazetteer,
    IReadOnlyList<string> SecondOpinions,
    bool Ablation,
    bool ClinicalPlaces,
    IReadOnlyList<string> CompareWith,
    ICorpusOutput? Output,
    string? MissProfileLabel)
{
    private static readonly Uri DefaultLlmEndpoint = new("http://127.0.0.1:11434/");

    public const string Usage =
        """
        Measures DeidSharp's detector stacks on an annotated corpus. Prints counts and rates only.

          --corpus physionet-deid --text <id.text> --gold <id-phi.phrase>
          --corpus asq-phi [--queries <synthetic_clinical_queries.txt>]   (default: the bundled copy)
          --corpus mimic-iv-note --notes <note/discharge.csv.gz>          (MIMIC-IV-Note 2.2, filled with surrogate PHI)

        Options:
          --scope safe-harbor|all    which gold spans count (default: safe-harbor)
          --split all|tune|report    which half: study and tune on one, publish from the other (default: all)
          --presidio <url>           also stack a stock Presidio analyzer (e.g. http://127.0.0.1:5002/)
          --clinical <url>           also stack the clinical sidecar (e.g. http://127.0.0.1:5003/)
          --misses <file>            write each missed span with its text, for you to read; refused inside a Git working tree
          --false-positives <file>   write each false positive with its text, for you to read; refused inside a Git working tree
          --miss-profile <label>     count each stack's misses of one corpus label by feature (one word, an ordinary word,
                                     found elsewhere in the note), counts only
          --publish <dir>            update docs/evaluation: results.json, its README's table, badge.json and, where it
                                     has the markers, the repository README's results at a glance; the run must measure
                                     the stacks they show
          --note <text>              published with the results, e.g. "clinical sidecar on CUDA"
          --drop-sources <a,b>       leave out the spans only these recognizers report, e.g. clinical:SpacyRecognizer;
                                     an experiment, so it can't be published
          --predictions <n=file,...> also score other tools' spans from files ("document<TAB>start<TAB>end[<TAB>category]")
          --compare-with <n,...>     also compare every stack with these prediction files, paired: the change and its 95% CI
          --variants <a,b>           also measure each stack with these changes: repeated-names; can't be published
          --llm <model,...>          also measure each stack with an LLM layer: an Ollama model, e.g. qwen3.5:9b; published with
                                     the model's digest and Ollama's version
          --llm-endpoint <url>       the Ollama server (default http://127.0.0.1:11434/); the notes go to it, so keep it local
          --llm-second-opinion <a,b> also measure each LLM stack with the model's second opinion: on (the other detectors' spans),
                                     self (also the model's own; an experiment, so it can't be published)
          --clinical-places on       also measure the clinical sidecar keeping only its locations and organizations
          --ablation on              also measure each Presidio service and model without the deterministic layer; can't be published
          --gazetteer <with,without> whether the deterministic layer has the gazetteer, or both families (default with)
          --llm-prompts <a,b>        the prompts each model is measured with: instructions, examples, neutral-examples, focused, focused-rules, retrieved, all-examples (default instructions); focused-rules and retrieved are
                                     experiments, so they can't be published

          --render <dir>             rewrite the tables and badge.json from results.json, measuring nothing
          --export <dir>             write the corpus (with --corpus, its files and --split) for other tools to read, measuring
                                     nothing: one text file per document, a deid input file and documents.tsv; refused inside
                                     a Git working tree
          --gold-review <file>       write every gold span with its label in context (with --corpus, its files and --split), for
                                     you to check the annotation, measuring nothing; refused inside a Git working tree
        """;

    public static EvaluationSettings Parse(IReadOnlyList<string> args)
    {
        var commandLine = new CommandLine(args);
        var corpus = CorpusCatalog.Create(commandLine.Required("corpus", $"({string.Join(", ", CorpusCatalog.Ids)})"), commandLine);

        var scopeId = commandLine.Optional("scope") ?? EvaluationScopes.Id(EvaluationScope.SafeHarbor);
        if (!EvaluationScopes.TryParse(scopeId, out var scope))
        {
            throw new UsageException($"--scope must be one of: {string.Join(", ", EvaluationScopes.Ids)}.");
        }

        var splitId = commandLine.Optional("split") ?? CorpusSplits.Id(CorpusSplit.All);
        if (!CorpusSplits.TryParse(splitId, out var split))
        {
            throw new UsageException($"--split must be one of: {string.Join(", ", CorpusSplits.Ids)}.");
        }

        // Writing the corpus out, for another tool or for a person, measures nothing, so any other option is refused.
        if (CorpusOutputs.Parse(commandLine) is { } corpusOutput)
        {
            commandLine.EnsureAllRead();
            return new EvaluationSettings(
                corpus, scope, split, null, null, null, null, null, null, new HashSet<string>(StringComparer.Ordinal), [], [], [], DefaultLlmEndpoint, [Cli.LlmPrompts.DefaultId], [true], [], false, false, [], corpusOutput, null);
        }

        var predictions = ParsePredictions(commandLine);
        var compareWith = ParseCompareWith(commandLine, predictions);
        var misses = ReviewFile(commandLine, MissesFile.Option, predictions);
        var falsePositives = ReviewFile(commandLine, FalsePositivesFile.Option, predictions);

        var dropped = ParseDroppedSources(commandLine);
        var variants = ParseVariants(commandLine);
        var publish = commandLine.Optional("publish");
        if (publish is not null && dropped.Count > 0)
        {
            throw new UsageException("--drop-sources measures a stack nobody runs, so it can't be published.");
        }

        var models = (commandLine.Optional("llm") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
        var gazetteer = ParseGazetteer(commandLine);
        var secondOpinions = ParseSecondOpinions(commandLine, models.Count > 0);
        var ablation = commandLine.Optional("ablation") switch
        {
            null => false,
            "on" => publish is null ? true : throw new UsageException("--ablation measures stacks nobody runs, so it can't be published."),
            _ => throw new UsageException("--ablation takes on."),
        };
        var clinicalPlaces = commandLine.Optional("clinical-places") switch
        {
            null => false,
            "on" => Endpoint(commandLine, "clinical") is not null ? true : throw new UsageException("--clinical-places needs --clinical."),
            _ => throw new UsageException("--clinical-places takes on."),
        };
        if (publish is not null && variants.Count > 0)
        {
            throw new UsageException("--variants measures experiments, so it can't be published.");
        }

        if (publish is not null && secondOpinions.FirstOrDefault(Cli.SecondOpinions.IsExperiment) is { } secondOpinion)
        {
            throw new UsageException($"--llm-second-opinion {secondOpinion} is an experiment, so it can't be published.");
        }

        var prompts = ParseLlmPrompts(commandLine);
        if (publish is not null && prompts.FirstOrDefault(Cli.LlmPrompts.IsExperiment) is { } prompt)
        {
            throw new UsageException($"--llm-prompts {prompt} is an experiment, so it can't be published.");
        }

        // The corpus's notes go to the model: credentialed notes must never leave this machine.
        var llmEndpoint = Endpoint(commandLine, "llm-endpoint") ?? DefaultLlmEndpoint;
        if (!llmEndpoint.IsLoopback)
        {
            throw new UsageException("--llm-endpoint must be on this machine (localhost): the corpus's notes are sent to it.");
        }

        if (publish is not null)
        {
            ResultsPublisher.EnsurePublishable(publish);
        }

        var missProfile = commandLine.Optional(MissProfile.Option);
        if (missProfile is not null && (string.IsNullOrWhiteSpace(missProfile) || missProfile.Any(char.IsControl)))
        {
            throw new UsageException($"--{MissProfile.Option} takes one corpus label, such as UntypedName.");
        }

        var note = commandLine.Optional("note");
        if (note is not null && (string.IsNullOrWhiteSpace(note) || note.Any(char.IsControl)))
        {
            throw new UsageException("--note must be one line of text.");
        }

        var settings = new EvaluationSettings(
            corpus, scope, split, Endpoint(commandLine, "presidio"), Endpoint(commandLine, "clinical"), misses, falsePositives, publish, note, dropped, predictions, variants, models, llmEndpoint, prompts, gazetteer, secondOpinions, ablation, clinicalPlaces, compareWith, null, missProfile);
        commandLine.EnsureAllRead();
        return settings;
    }

    private static HashSet<string> ParseDroppedSources(CommandLine commandLine)
    {
        var names = (commandLine.Optional("drop-sources") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return names.All(n => n.IndexOf(':', StringComparison.Ordinal) is var colon and > 0 && colon < n.Length - 1)
            ? names.ToHashSet(StringComparer.Ordinal)
            : throw new UsageException("--drop-sources takes recognizers as the report names them, such as clinical:SpacyRecognizer.");
    }

    private static List<bool> ParseGazetteer(CommandLine commandLine)
    {
        var ids = (commandLine.Optional("gazetteer") ?? "with").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Length > 0 && ids.All(id => id is "with" or "without") && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
            ? [.. ids.Select(id => id == "with")]
            : throw new UsageException("--gazetteer takes with, without, or both.");
    }

    private static List<string> ParseLlmPrompts(CommandLine commandLine)
    {
        var ids = (commandLine.Optional("llm-prompts") ?? Cli.LlmPrompts.DefaultId).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Length > 0 && ids.All(Cli.LlmPrompts.Knows) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
            ? [.. ids]
            : throw new UsageException($"--llm-prompts takes a list of: {string.Join(", ", Cli.LlmPrompts.Ids)}.");
    }

    private static List<string> ParseSecondOpinions(CommandLine commandLine, bool hasModel)
    {
        var ids = (commandLine.Optional("llm-second-opinion") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!ids.All(Cli.SecondOpinions.Knows) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new UsageException($"--llm-second-opinion takes a list of: {string.Join(", ", Cli.SecondOpinions.Ids)}.");
        }

        return ids.Length == 0 || hasModel ? [.. ids] : throw new UsageException("--llm-second-opinion needs --llm: the model gives the opinion.");
    }

    private static List<string> ParseVariants(CommandLine commandLine)
    {
        var ids = (commandLine.Optional("variants") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.All(StackVariants.Knows) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
            ? [.. ids]
            : throw new UsageException($"--variants takes a list of: {string.Join(", ", StackVariants.Ids)}.");
    }

    private static List<(string Name, string Path)> ParsePredictions(CommandLine commandLine)
    {
        var predictions = new List<(string Name, string Path)>();
        foreach (var entry in (commandLine.Optional("predictions") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = entry.IndexOf('=', StringComparison.Ordinal);
            var name = equals > 0 ? entry[..equals] : "";
            if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_') || name.Length == 0 || equals == entry.Length - 1)
            {
                throw new UsageException("--predictions takes name=file pairs, such as deid=deid-spans.tsv, with names of letters, digits, '-', '.' and '_'.");
            }

            if (predictions.Any(p => p.Name == name) || name.StartsWith("deterministic", StringComparison.Ordinal))
            {
                throw new UsageException($"--predictions names a stack \"{name}\" twice, or like one of DeidSharp's.");
            }

            predictions.Add((name, entry[(equals + 1)..]));
        }

        return predictions;
    }

    private static List<string> ParseCompareWith(CommandLine commandLine, List<(string Name, string Path)> predictions)
    {
        var names = (commandLine.Optional("compare-with") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return names.All(n => predictions.Any(p => p.Name == n)) && names.Distinct(StringComparer.Ordinal).Count() == names.Length
            ? [.. names]
            : throw new UsageException("--compare-with takes names given to --predictions, each once.");
    }

    private static string? ReviewFile(CommandLine commandLine, string option, List<(string Name, string Path)> predictions)
    {
        var path = commandLine.Optional(option);
        if (path is null)
        {
            return null;
        }

        ReviewFiles.EnsureOutsideRepository(path, option);

        // A typo must not overwrite a corpus file or the other review file.
        return commandLine.Values(except: option).Concat(predictions.Select(p => p.Path)).Any(v => ReviewFiles.SameFile(v, path))
            ? throw new UsageException($"--{option} names a file another option uses too.")
            : path;
    }

    private static Uri? Endpoint(CommandLine commandLine, string name)
    {
        var value = commandLine.Optional(name);
        if (value is null)
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw new UsageException($"--{name} must be an absolute http or https address.");
    }
}
