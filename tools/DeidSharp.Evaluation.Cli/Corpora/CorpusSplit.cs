using System.Security.Cryptography;
using System.Text;

namespace DeidSharp.Evaluation.Cli.Corpora;

/// <summary>
/// Which half of a corpus a run reads. Changes to detectors are tuned on one half and
/// reported on the other, so a published number is never measured on the notes it was
/// tuned on.
/// </summary>
internal enum CorpusSplit
{
    /// <summary>Every document.</summary>
    All,

    /// <summary>The half to study misses and false positives on, and tune against.</summary>
    Tune,

    /// <summary>The held-out half that published numbers come from.</summary>
    Report,
}

/// <summary>The splits' names, and which half each document falls in.</summary>
internal static class CorpusSplits
{
    private static readonly Dictionary<string, CorpusSplit> ById = new(StringComparer.Ordinal)
    {
        ["all"] = CorpusSplit.All,
        ["tune"] = CorpusSplit.Tune,
        ["report"] = CorpusSplit.Report,
    };

    public static IEnumerable<string> Ids => ById.Keys;

    public static bool TryParse(string id, out CorpusSplit split) => ById.TryGetValue(id, out split);

    public static string Id(CorpusSplit split) => ById.Single(kv => kv.Value == split).Key;

    /// <summary>
    /// True if a document of <paramref name="group"/> belongs to <paramref name="split"/>.
    /// Whole groups (a patient's notes) fall on one side, so a patient's names never help
    /// on the other. The side comes from a hash of the group's name: the same on every
    /// machine and in every release, never to be changed, or tuned notes would leak into
    /// the reported half.
    /// </summary>
    public static bool Includes(CorpusSplit split, string group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (split == CorpusSplit.All)
        {
            return true;
        }

        var tune = (SHA256.HashData(Encoding.UTF8.GetBytes("deidsharp-split:" + group))[0] & 1) == 0;
        return tune == (split == CorpusSplit.Tune);
    }
}
