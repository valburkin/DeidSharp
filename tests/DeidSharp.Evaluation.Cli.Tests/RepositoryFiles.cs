namespace DeidSharp.Evaluation.Cli.Tests;

/// <summary>Files of this repository, found from the test's output directory.</summary>
internal static class RepositoryFiles
{
    private static string Root { get; } = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        for (var directory = AppContext.BaseDirectory; directory is not null; directory = System.IO.Path.GetDirectoryName(directory))
        {
            if (File.Exists(System.IO.Path.Combine(directory, "DeidSharp.slnx")))
            {
                return directory;
            }
        }

        throw new InvalidOperationException("The tests run outside the repository: DeidSharp.slnx not found.");
    }
}
