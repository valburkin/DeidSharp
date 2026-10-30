namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// The language models a measurement asked, pinned the way a sidecar image is by its digest:
/// Ollama's version and each model's digest, since a tag such as <c>gemma4:26b-a4b</c> can
/// point to other weights after a pull.
/// </summary>
/// <param name="Ollama">The Ollama server's version.</param>
internal sealed record LanguageModels(string Ollama, IReadOnlyList<ModelPin> Models)
{
    /// <summary>Whether <paramref name="other"/> names the same server version and the same weights, in the same order.</summary>
    public bool SameAs(LanguageModels other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Ollama == other.Ollama && Models.SequenceEqual(other.Models);
    }

    /// <summary>Refuses to publish when the pins read after a run differ from these, read before it.</summary>
    public void EnsureUnchanged(LanguageModels? after)
    {
        if (after is null || !SameAs(after))
        {
            throw new UsageException("Ollama or a model changed during the run, so its results are not published. Run it again.");
        }
    }

    /// <summary>One line for the results page: each model with its digest's first 12 characters, as <c>ollama list</c> shows it.</summary>
    public string Describe() =>
        $"{string.Join(", ", Models.Select(m => $"{m.Name} (`{m.Digest[..Math.Min(12, m.Digest.Length)]}`)"))} on Ollama {Ollama}";
}

/// <summary>An Ollama model by its name and the digest of the weights the name pointed to.</summary>
internal sealed record ModelPin(string Name, string Digest);
