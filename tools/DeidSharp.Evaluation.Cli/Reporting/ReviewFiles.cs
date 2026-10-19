namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// What the files a person reads after a run (the misses, the false positives) share: they
/// hold corpus text, so they are refused inside a Git working tree, and each span is shown
/// on one line inside its context. A restricted corpus must never be committed (PhysioNet's
/// data use agreement forbids sharing it); nothing else the tool writes holds text.
/// </summary>
internal static class ReviewFiles
{
    private const int Context = 40;

    public static void EnsureOutsideRepository(string path, string option)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var directory = Path.GetDirectoryName(Path.GetFullPath(path)); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            var git = Path.Combine(directory, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                throw new UsageException($"--{option} must point outside any Git working tree: the file holds corpus text, which must never be committed.");
            }
        }
    }

    /// <summary>The span marked [[like this]] with up to 40 characters on each side, on one line.</summary>
    public static string InContext(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        var before = text[Math.Max(0, start - Context)..start];
        var after = text[end..Math.Min(text.Length, end + Context)];
        return $"{OneLine(before)}[[{OneLine(text[start..end])}]]{OneLine(after)}";
    }

    private static string OneLine(string text) => text.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
}
