namespace DeidSharp.Evaluation.Cli.Tests;

/// <summary>A fresh directory under the system's temp folder (outside any Git working tree), deleted after the test.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("deidsharp-eval-").FullName;

    public string File(string name, string content)
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
