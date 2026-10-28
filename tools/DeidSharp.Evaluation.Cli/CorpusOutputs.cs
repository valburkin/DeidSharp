using DeidSharp.Evaluation.Cli.Predictions;
using DeidSharp.Evaluation.Cli.Reporting;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each option that writes the corpus out instead of measuring it is one row, its
// name and how to make its ICorpusOutput; a new output is a new row, never a branch in
// the settings or the run.

/// <summary>The options that write the corpus out instead of measuring it.</summary>
internal static class CorpusOutputs
{
    private static readonly (string Option, Func<string, ICorpusOutput> Create)[] All =
    [
        (CorpusExport.Option, directory => new CorpusExport(directory)),
        (GoldReviewFile.Option, path => new GoldReviewFile(path)),
    ];

    /// <summary>The output the command line asks for, if any; two at once are refused.</summary>
    public static ICorpusOutput? Parse(CommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        var chosen = All.Select(o => (o.Option, o.Create, Value: commandLine.Optional(o.Option))).Where(o => o.Value is not null).ToList();
        return chosen switch
        {
            [] => null,

            // A typo must not overwrite a corpus file.
            [var one] when commandLine.Values(except: one.Option).Any(v => ReviewFiles.SameFile(v, one.Value!)) =>
                throw new UsageException($"--{one.Option} names a file another option uses too."),
            [var one] => one.Create(one.Value!),
            _ => throw new UsageException($"{string.Join(" and ", chosen.Select(o => "--" + o.Option))} are separate runs."),
        };
    }
}
