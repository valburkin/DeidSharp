namespace DeidSharp.Llm;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: how the LLM detector picks the examples it shows the model for a piece of text, so
// a retriever by word similarity and one by embeddings can be swapped without changing it.
// The libraries ship none; the evaluation tool holds the one that was measured.

/// <summary>Picks worked examples for a text, so the model sees the ones most like what it reads.</summary>
public interface IExampleRetriever
{
    /// <summary>Up to <paramref name="count"/> examples for <paramref name="text"/>, most similar first.</summary>
    IReadOnlyList<LlmExample> Retrieve(string text, int count);
}
