using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>
/// The JSON format of the published files, source-generated: camelCase names, indented,
/// "\n" line endings on every platform so the committed files don't change with the OS.
/// </summary>
[JsonSerializable(typeof(PublishedResults))]
[JsonSerializable(typeof(ShieldsBadge))]
internal sealed partial class ResultsJson : JsonSerializerContext
{
    /// <summary>
    /// The options the files are written with. Characters such as "+" and "·" stay as they
    /// are instead of \u escapes: the files are read as data and by people, never put in HTML.
    /// </summary>
    public static ResultsJson Files { get; } = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
}
