using DeidSharp.Llm;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each prompt an LLM layer can be measured with is one row, so a new prompt variant
// (gazetteer hints, retrieved examples) is a row, never a branch in DetectorStacks, and
// the row says whether it is an experiment, which --publish refuses.

/// <summary>The prompts <c>--llm-prompts</c> can name, so their gains can be compared in one run.</summary>
internal static class LlmPrompts
{
    public const string DefaultId = "instructions";

    private static readonly Lazy<IExampleRetriever> Pool = new(() => new TfIdfExampleRetriever(LlmExamplePool.Default));

    // The library's prompts as it ships them are publishable; a prompt built only here is an experiment.
    private static readonly Dictionary<string, (LlmPrompt Prompt, Func<IExampleRetriever>? Retriever, int? ContextWindow, bool Experiment)> Rows = new(StringComparer.Ordinal)
    {
        [DefaultId] = (LlmPrompt.Default, null, null, false),
        ["examples"] = (LlmPrompt.WithExamples, null, null, false),
        ["neutral-examples"] = (LlmPrompt.WithNeutralExamples, null, null, false),
        ["focused"] = (LlmPrompt.Focused, null, null, false),

        // An ablation: the focused instructions with the first examples, to tell the two changes apart.
        ["focused-rules"] = (LlmPrompt.Focused with { Examples = LlmPrompt.WithExamples.Examples }, null, null, true),

        // H2: the instructions with four examples picked for each piece of text from the fictional pool,
        // against "neutral-examples", the same instructions with four fixed ones. The retriever stays
        // in this tool (TfIdfExampleRetriever, F27).
        ["retrieved"] = (LlmPrompt.Default, () => Pool.Value, null, true),

        // H2, third arm: every example of the pool in every prompt (about 5,000 tokens), so the
        // model's window is raised past Ollama's 4,096-token default, which would cut the prompt's start.
        ["all-examples"] = (LlmPrompt.WithPoolExamples, null, 8_192, false),
    };

    public static IEnumerable<string> Ids => Rows.Keys;

    public static bool Knows(string id) => Rows.ContainsKey(id);

    /// <summary>Whether the prompt is an experiment, so its results can't be published.</summary>
    public static bool IsExperiment(string id) => Rows[id].Experiment;

    public static (LlmPrompt Prompt, IExampleRetriever? Retriever, int? ContextWindow) Get(string id)
    {
        var (prompt, retriever, window, _) = Rows[id];
        return (prompt, retriever?.Invoke(), window);
    }
}
