using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeidSharp.Llm;

/// <summary>
/// Fictional annotated clinical notes to retrieve examples from: forty short notes written for
/// DeidSharp in several styles (ICU nursing notes in capitals, progress notes, discharge
/// summaries, phone and social work notes, radiology), twelve with no PHI at all, so the model
/// sees what isn't PHI as often as what is. None comes from a real record.
/// </summary>
public static partial class LlmExamplePool
{
    private static readonly Lazy<IReadOnlyList<LlmExample>> Loaded = new(Load);

    public static IReadOnlyList<LlmExample> Default => Loaded.Value;

    private static IReadOnlyList<LlmExample> Load()
    {
        using var stream = typeof(LlmExamplePool).Assembly.GetManifestResourceStream("DeidSharp.Llm.Data.example-pool.json")
            ?? throw new InvalidOperationException("The example pool is missing from the assembly.");
        var entries = JsonSerializer.Deserialize(stream, PoolJson.Default.ListPoolEntry)
            ?? throw new InvalidOperationException("The example pool is empty.");
        return [.. entries.Select(e => new LlmExample(e.Text, [.. e.Phi.Select(p => new LlmFinding(p.Text, p.Type))]))];
    }

    internal sealed record PoolEntry(string Text, IReadOnlyList<PoolFinding> Phi);

    internal sealed record PoolFinding(string Text, string Type);

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(List<PoolEntry>))]
    internal sealed partial class PoolJson : JsonSerializerContext;
}
