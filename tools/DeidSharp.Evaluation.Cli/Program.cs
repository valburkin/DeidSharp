using System.Text.Json;
using DeidSharp.Detection;
using DeidSharp.Evaluation.Cli;
using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Reporting;

// Measures DeidSharp's detector stacks on an annotated corpus (docs/evaluation/README.md).
// Everything it prints is counts and rates. Corpus text goes only to the files a person or
// another tool reads (--misses, --false-positives, --export, --gold-review), which are
// refused inside a Git working tree.

if (args.Length == 0 || args is ["--help"] or ["-h"])
{
    Console.WriteLine(EvaluationSettings.Usage);
    return 0;
}

if (args is ["--render", var directory])
{
    try
    {
        ResultsPublisher.EnsurePublishable(directory);
        var notice = ResultsPublisher.Render(directory);
        Console.WriteLine($"Rendered {Path.GetFullPath(directory)} from its {ResultsPublisher.ResultsFile}.");
        if (notice is not null)
        {
            Console.WriteLine(notice);
        }
        return 0;
    }
    catch (Exception e) when (e is UsageException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(e.Message);
        return 2;
    }
    catch (JsonException)
    {
        Console.Error.WriteLine($"{ResultsPublisher.ResultsFile} is not valid; restore it from Git.");
        return 2;
    }
}

using var cancel = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, e) =>
{
    e.Cancel = true;
    // The finally block unsubscribes before the token source is disposed.
    // ReSharper disable once AccessToDisposedClosure
    cancel.Cancel();
};
Console.CancelKeyPress += onCancel;

try
{
    var settings = EvaluationSettings.Parse(args);
    await EvaluationRun.RunAsync(settings, Console.Out, Console.Error, DateOnly.FromDateTime(DateTime.UtcNow), cancel.Token);
    return 0;
}
catch (Exception e) when (e is UsageException or CorpusFormatException or PhiDetectionException or IOException or UnauthorizedAccessException)
{
    // These messages carry positions, paths and categories, never corpus text.
    Console.Error.WriteLine(e.Message);
    return 2;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Canceled.");
    return 130;
}
catch (Exception e)
{
    // Another library's message may quote what it was parsing; name the failure only.
    Console.Error.WriteLine($"The run failed with {e.GetType().FullName}.");
    return 1;
}
finally
{
    // A Ctrl+C after the run must not reach the disposed token source.
    Console.CancelKeyPress -= onCancel;
}
