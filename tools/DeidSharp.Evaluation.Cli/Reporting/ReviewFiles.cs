namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// What the files a person reads (the misses, the false positives, the gold review) share:
/// they hold corpus text, so they are refused inside a Git working tree, and each span is
/// shown on one line inside its context. A restricted corpus must never be committed
/// (PhysioNet's data use agreement forbids sharing it); of the rest the tool writes, only
/// --export holds text, and it is refused there too.
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

    /// <summary>Whether an option's value names the same file as <paramref name="path"/>, so a typo can't overwrite a corpus file.</summary>
    /// <remarks>Other options' values aren't all paths (an address, a note); one that isn't can't be the same file.</remarks>
    public static bool SameFile(string value, string path)
    {
        try
        {
            return string.Equals(Path.GetFullPath(value), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
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
