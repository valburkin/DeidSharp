using System.Text;
using System.Text.Json;

namespace DeidSharp.Evaluation.Cli.Reporting;

// Design pattern: Facade.
// The pattern: one simple entry point in front of several cooperating parts, so
// callers don't have to wire them together themselves.
// Here: publishing a measurement updates the files that must agree (results.json, the
// evaluation README's table, badge.json, and the repository README's results at a glance);
// the run makes one call and they stay in step.

/// <summary>Writes a measurement into docs/evaluation.</summary>
internal static class ResultsPublisher
{
    public const string ResultsFile = "results.json";
    public const string BadgeFile = "badge.json";
    public const string PageFile = "README.md";

    /// <summary>
    /// The repository's README, when <paramref name="directory"/> is its docs/evaluation, two
    /// folders below it; null for any other directory, so publishing never reaches outside one it wasn't given.
    /// </summary>
    public static string? GlancePage(string directory)
    {
        var evaluation = new DirectoryInfo(Path.GetFullPath(directory));
        return evaluation is { Parent: { Parent: { } root } docs }
               && string.Equals(evaluation.Name, "evaluation", StringComparison.OrdinalIgnoreCase)
               && string.Equals(docs.Name, "docs", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root.FullName, PageFile)
            : null;
    }

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Refuses a directory that publishing would fail on, before a long run starts.</summary>
    public static void EnsurePublishable(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var page = Path.Combine(directory, PageFile);
        if (!File.Exists(page) || !ResultsPage.HasSection(File.ReadAllText(page, Utf8)))
        {
            throw new UsageException($"--publish needs a directory whose {PageFile} holds the results markers (docs/evaluation).");
        }

        // A lost glance marker or a broken results.json is refused here, not after a run of hours.
        _ = ReadGlance(GlancePage(directory));
        _ = Load(directory);
    }

    /// <summary>
    /// Publishes <paramref name="result"/>. Returns a line to show the user when the repository README has no glance
    /// markers, so its table was left alone; otherwise null.
    /// </summary>
    public static string? Publish(string directory, CorpusResult result)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return Write(directory, Load(directory).With(result));
    }

    /// <summary>
    /// Rewrites the table and the badge from results.json as it stands, measuring nothing: after a change to how they are
    /// shown. Returns a line to show the user when the repository README has no glance markers; otherwise null.
    /// </summary>
    public static string? Render(string directory) => Write(directory, Load(directory));

    /// <summary>The README's text when it has the glance markers, null when it has none; a lost marker is refused.</summary>
    private static string? ReadGlance(string? glancePath) =>
        glancePath is not null && File.Exists(glancePath) && File.ReadAllText(glancePath, Utf8) is var readme && ResultsGlance.HasSection(readme)
            ? readme
            : null;

    private static string? Write(string directory, PublishedResults results)
    {
        var pagePath = Path.Combine(directory, PageFile);

        // The repository README is updated where it has the glance markers; a directory elsewhere has none.
        var glancePath = GlancePage(directory);
        var glance = ReadGlance(glancePath) is { } readme ? ResultsGlance.Update(readme, results) : null;

        // Everything is rendered before anything is written, so a failure leaves the files as they were.
        var page = ResultsPage.Update(File.ReadAllText(pagePath, Utf8), results);
        var resultsJson = ResultsJsonText(results);
        var badgeJson = BadgeJsonText(results);

        File.WriteAllText(Path.Combine(directory, ResultsFile), resultsJson, Utf8);
        File.WriteAllText(Path.Combine(directory, BadgeFile), badgeJson, Utf8);
        File.WriteAllText(pagePath, page, Utf8);
        if (glance is null)
        {
            return glancePath is null ? null : "The repository README has no results-at-a-glance markers, so its table was left as it is.";
        }

        File.WriteAllText(glancePath!, glance, Utf8);
        return null;
    }

    public static PublishedResults Load(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var path = Path.Combine(directory, ResultsFile);
        if (!File.Exists(path))
        {
            return new PublishedResults([]);
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path, Utf8), ResultsJson.Files.PublishedResults)
                   ?? throw new UsageException($"{ResultsFile} is empty.");
        }
        catch (JsonException)
        {
            throw new UsageException($"{ResultsFile} is not valid; restore it from Git.");
        }
    }

    public static string ResultsJsonText(PublishedResults results) => JsonSerializer.Serialize(results, ResultsJson.Files.PublishedResults) + "\n";

    public static string BadgeJsonText(PublishedResults results) => JsonSerializer.Serialize(ShieldsBadge.From(results), ResultsJson.Files.ShieldsBadge) + "\n";
}
