using DeidSharp.Llm;

namespace DeidSharp.Tests.TestDoubles;

// Design pattern: Test Double (Stub).
// The pattern: a stand-in for a real dependency in a test. A stub returns canned answers.
// Here: a retriever whose answer is the start of its list, whatever the text, so a test
// can check where the LLM detector puts retrieved examples without a real retriever.

/// <summary>Returns the first examples of its list, whatever the text.</summary>
internal sealed class FixedExampleRetriever(IReadOnlyList<LlmExample> examples) : IExampleRetriever
{
    public IReadOnlyList<LlmExample> Retrieve(string text, int count) => [.. examples.Take(count)];
}
