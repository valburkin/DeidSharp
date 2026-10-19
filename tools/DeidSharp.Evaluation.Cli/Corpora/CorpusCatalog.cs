namespace DeidSharp.Evaluation.Cli.Corpora;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each corpus the tool knows is one row: its id and how to build its reader from
// the command line. A new corpus is a new row, never a branch in the settings parser.

/// <summary>The corpora the tool can read, by command-line id.</summary>
internal static class CorpusCatalog
{
    private static readonly (string Id, Func<CommandLine, ICorpus> Create)[] Entries =
    [
        ("physionet-deid", c => new PhysioNetDeidCorpus(
            c.Required("text", "for physionet-deid (the notes, id.text)"),
            c.Required("gold", "for physionet-deid (the gold spans, id-phi.phrase from the deid package)"))),
        ("asq-phi", c => new AsqPhiCorpus(c.Optional("queries") ?? AsqPhiCorpus.BundledPath)),
    ];

    public static IEnumerable<string> Ids => Entries.Select(e => e.Id);

    public static ICorpus Create(string id, CommandLine commandLine)
    {
        foreach (var (entryId, create) in Entries)
        {
            if (entryId == id)
            {
                return create(commandLine);
            }
        }

        throw new UsageException($"--corpus must be one of: {string.Join(", ", Ids)}.");
    }
}
