using Microsoft.Extensions.AI;

namespace DeidSharp.Llm;

/// <summary>Tuning for <see cref="LlmPhiDetector"/>.</summary>
public sealed record LlmDetectorOptions
{
    public static LlmDetectorOptions Default { get; } = new();

    /// <summary>The detector's name, recorded as the source of its findings. Default "llm".</summary>
    public string Name { get; init; } = "llm";

    /// <summary>What the model is told. Default <see cref="LlmPrompt.Default"/>.</summary>
    public LlmPrompt Prompt { get; init; } = LlmPrompt.Default;

    /// <summary>
    /// Picks examples for each piece of text from a pool, shown after the prompt's own examples;
    /// null shows the prompt's examples only. An extension point: the libraries ship no
    /// retriever, because a retriever by word similarity added precision but not recall in the
    /// measurements, and <see cref="LlmPrompt.WithPoolExamples"/> did better.
    /// </summary>
    public IExampleRetriever? Retriever { get; init; }

    /// <summary>How many examples <see cref="Retriever"/> adds for each piece of text (1-20). Default 4.</summary>
    public int RetrievedExamples { get; init; } = 4;

    /// <summary>
    /// The longest piece of text sent in one call, in characters (500–100,000). A longer text
    /// goes in overlapping pieces, so a small model's attention isn't spread over a whole
    /// record. Default 2,000.
    /// </summary>
    public int ChunkLength { get; init; } = 2_000;

    /// <summary>How many characters consecutive pieces share, so PHI cut at a piece's edge is whole in the next (0 to half the chunk). Default 200.</summary>
    public int ChunkOverlap { get; init; } = 200;

    /// <summary>The most the model may write for one piece of text. Default 2,048 tokens.</summary>
    public int MaxOutputTokens { get; init; } = 2_048;

    /// <summary>
    /// Drops what the model lists that Safe Harbor doesn't ask to remove, however it typed it:
    /// an age under 90; as a date, a year or a time on its own ("2019", "0800", "8pm") or words with
    /// no number or month ("today", "Monday"); and an identifier of three digits or fewer (a bed, a
    /// room, a setting). Default true.
    /// </summary>
    public bool SafeHarborOnly { get; init; } = true;

    /// <summary>
    /// Sets what a provider needs beyond the detector's own settings, on each request after them.
    /// For Ollama, turn a reasoning model's thinking off, or a note takes a minute instead of a
    /// second: <c>o.RawRepresentationFactory = _ => new OllamaSharp.Models.Chat.ChatRequest { Think = false }</c>.
    /// </summary>
    public Action<ChatOptions>? ConfigureRequest { get; init; }

    /// <exception cref="ArgumentException">A value is out of range, or the name is blank.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentNullException.ThrowIfNull(Prompt);
        ArgumentOutOfRangeException.ThrowIfLessThan(ChunkLength, 500);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ChunkLength, 100_000);
        ArgumentOutOfRangeException.ThrowIfNegative(ChunkOverlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ChunkOverlap, ChunkLength / 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOutputTokens, 64);
        ArgumentOutOfRangeException.ThrowIfLessThan(RetrievedExamples, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(RetrievedExamples, 20);
    }
}
