namespace DeidSharp.Evaluation.Cli.Corpora;

/// <summary>Which of a corpus's gold spans a run counts.</summary>
internal enum EvaluationScope
{
    /// <summary>
    /// What DeidSharp removes under Safe Harbor. Corpus labels Safe Harbor lets stay (a
    /// year on its own, for example) are left out. Staff names stay in: DeidSharp removes
    /// every name (<see cref="PhiCategory.Name"/>).
    /// </summary>
    SafeHarbor,

    /// <summary>Every span the corpus annotates.</summary>
    All,
}

/// <summary>The scopes' names on the command line and in the results file.</summary>
internal static class EvaluationScopes
{
    private static readonly Dictionary<string, EvaluationScope> ById = new(StringComparer.Ordinal)
    {
        ["safe-harbor"] = EvaluationScope.SafeHarbor,
        ["all"] = EvaluationScope.All,
    };

    public static IEnumerable<string> Ids => ById.Keys;

    public static bool TryParse(string id, out EvaluationScope scope) => ById.TryGetValue(id, out scope);

    public static string Id(EvaluationScope scope) => ById.Single(kv => kv.Value == scope).Key;
}
