using System.Text;
using System.Text.Json;

namespace DeidSharp.Evaluation.Cli.Reporting;

// Design pattern: Facade.
// The pattern: one simple entry point in front of several cooperating parts, so
// callers don't have to wire them together themselves.
// Here: publishing a measurement updates three files that must agree (results.json,
// the README's table, badge.json); the run makes one call and they stay in step.

/// <summary>Writes a measurement into docs/evaluation.</summary>
internal static class ResultsPublisher
{
    public const string ResultsFile = "results.json";
    public const string BadgeFile = "badge.json";
    public const string PageFile = "README.md";

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
    }

    public static void Publish(string directory, CorpusResult result)
    {
        ArgumentNullException.ThrowIfNull(directory);
        Write(directory, Load(directory).With(result));
    }

    /// <summary>Rewrites the table and the badge from results.json as it stands, measuring nothing: after a change to how they are shown.</summary>
    public static void Render(string directory) => Write(directory, Load(directory));

    private static void Write(string directory, PublishedResults results)
    {
        var pagePath = Path.Combine(directory, PageFile);

        // Everything is rendered before anything is written, so a failure leaves the three files as they were.
        var page = ResultsPage.Update(File.ReadAllText(pagePath, Utf8), results);
        var resultsJson = ResultsJsonText(results);
        var badgeJson = BadgeJsonText(results);

        File.WriteAllText(Path.Combine(directory, ResultsFile), resultsJson, Utf8);
        File.WriteAllText(Path.Combine(directory, BadgeFile), badgeJson, Utf8);
        File.WriteAllText(pagePath, page, Utf8);
    }

    public static PublishedResults Load(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var path = Path.Combine(directory, ResultsFile);
        return File.Exists(path)
            ? JsonSerializer.Deserialize(File.ReadAllText(path, Utf8), ResultsJson.Files.PublishedResults)
              ?? throw new UsageException($"{ResultsFile} is empty.")
            : new PublishedResults([]);
    }

    public static string ResultsJsonText(PublishedResults results) => JsonSerializer.Serialize(results, ResultsJson.Files.PublishedResults) + "\n";

    public static string BadgeJsonText(PublishedResults results) => JsonSerializer.Serialize(ShieldsBadge.From(results), ResultsJson.Files.ShieldsBadge) + "\n";
}
