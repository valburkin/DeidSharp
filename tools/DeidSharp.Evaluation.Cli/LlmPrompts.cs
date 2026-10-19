using DeidSharp.Llm;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each prompt an LLM layer can be measured with is one row, so a new prompt variant
// (gazetteer hints, retrieved examples) is a row, never a branch in DetectorStacks.

/// <summary>The prompts <c>--llm-prompts</c> can name, so their gains can be compared in one run.</summary>
internal static class LlmPrompts
{
    public const string DefaultId = "instructions";

    private static readonly Lazy<IExampleRetriever> Pool = new(() => new TfIdfExampleRetriever(LlmExamplePool.Default));

    private static readonly Dictionary<string, (LlmPrompt Prompt, Func<IExampleRetriever>? Retriever, int? ContextWindow)> Rows = new(StringComparer.Ordinal)
    {
        [DefaultId] = (LlmPrompt.Default, null, null),
        ["examples"] = (LlmPrompt.WithExamples, null, null),
        ["neutral-examples"] = (LlmPrompt.WithNeutralExamples, null, null),
        ["focused"] = (LlmPrompt.Focused, null, null),

        // An ablation: the focused instructions with the first examples, to tell the two changes apart.
        ["focused-rules"] = (LlmPrompt.Focused with { Examples = LlmPrompt.WithExamples.Examples }, null, null),

        // H2: the instructions with four examples picked for each piece of text from the fictional pool,
        // against "neutral-examples", the same instructions with four fixed ones.
        ["retrieved"] = (LlmPrompt.Default, () => Pool.Value, null),

        // H2, third arm: every example of the pool in every prompt (about 5,000 tokens), so the
        // model's window is raised past Ollama's 4,096-token default, which would cut the prompt's start.
        ["all-examples"] = (LlmPrompt.WithPoolExamples, null, 8_192),
    };

    public static IEnumerable<string> Ids => Rows.Keys;

    public static bool Knows(string id) => Rows.ContainsKey(id);

    public static (LlmPrompt Prompt, IExampleRetriever? Retriever, int? ContextWindow) Get(string id)
    {
        var (prompt, retriever, window) = Rows[id];
        return (prompt, retriever?.Invoke(), window);
    }
}
