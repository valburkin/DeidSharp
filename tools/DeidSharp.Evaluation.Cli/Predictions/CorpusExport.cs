using System.Globalization;
using System.Text;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;

namespace DeidSharp.Evaluation.Cli.Predictions;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: one ICorpusOutput, the one --export picks: the corpus in the forms other
// tools read. The run writes whichever output the command line chose.

/// <summary>
/// Writes a corpus's documents in the forms other de-identification tools read, so they can be
/// run on exactly the text this tool scores and their spans converted back
/// (<c>tools/baselines/to-predictions.cs</c>). Each document gets a key, its number in the
/// corpus's order ("00001"):
/// <list type="bullet">
/// <item><c>documents.tsv</c>: each key and its document's id in the corpus, positions only.</item>
/// <item><c>text/&lt;key&gt;.txt</c>: the document's text, for tools that read a folder of notes (Philter).</item>
/// <item><c>deid.text</c>: every document as one deid record (patient = the key's number, note 1).</item>
/// </list>
/// The files hold corpus text, so the directory must be outside any Git working tree, and new or empty.
/// </summary>
internal sealed class CorpusExport : ICorpusOutput
{
    public const string Option = "export";
    public const string IndexFile = "documents.tsv";
    public const string TextFolder = "text";
    public const string DeidFile = "deid.text";

    // Philter guesses each file's encoding; a byte order mark makes the guess UTF-8 for certain,
    // so its offsets count the same characters this tool reads.
    private static readonly UTF8Encoding Utf8WithMark = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <param name="directory">The new or empty directory to write to, outside any Git working tree.</param>
    public CorpusExport(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ReviewFiles.EnsureOutsideRepository(Path.Combine(directory, IndexFile), Option);
        Destination = directory;
    }

    public string Destination { get; }

    public async Task WriteAsync(IReadOnlyList<CorpusDocument> documents, TextWriter console)
    {
        ArgumentNullException.ThrowIfNull(console);
        Write(Destination, documents);
        await console.WriteLineAsync(Formats.Invariant($"Wrote {documents.Count:N0} documents to {Path.GetFullPath(Destination)}. They hold corpus text: keep the folder out of Git."));
    }

    public static void Write(string directory, IReadOnlyList<CorpusDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(documents);
        ReviewFiles.EnsureOutsideRepository(Path.Combine(directory, IndexFile), Option);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new UsageException("--export needs a new or empty directory, so no earlier export is mixed in.");
        }

        Directory.CreateDirectory(Path.Combine(directory, TextFolder));
        var width = Math.Max(5, documents.Count.ToString(CultureInfo.InvariantCulture).Length);
        var index = new StringBuilder("# key\tdocument\n");
        var deid = new StringBuilder();
        for (var i = 0; i < documents.Count; i++)
        {
            var number = i + 1;
            var key = number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
            var document = documents[i];
            index.Append(key).Append('\t').Append(document.Id).Append('\n');
            File.WriteAllText(Path.Combine(directory, TextFolder, key + ".txt"), document.Text, Utf8WithMark);

            // deid ignores text on the end-of-record line, so the marker gets a line of its own.
            deid.Append(CultureInfo.InvariantCulture, $"START_OF_RECORD={number}||||1||||\n").Append(document.Text).Append("\n||||END_OF_RECORD\n");
        }

        File.WriteAllText(Path.Combine(directory, IndexFile), index.ToString(), Utf8);
        File.WriteAllText(Path.Combine(directory, DeidFile), deid.ToString(), Utf8);
    }
}
