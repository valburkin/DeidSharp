using System.Reflection;

namespace DeidSharp.Evaluation.Cli.Reporting;

/// <summary>Which DeidSharp a measurement was made with: the package version and the Git commit it was built from.</summary>
internal sealed record BuildVersion(string Version, string Commit)
{
    /// <summary>
    /// The core library's informational version, which the SDK writes as "0.1.0+commit"
    /// when it builds from a Git checkout.
    /// </summary>
    public static BuildVersion OfLibrary()
    {
        var informational = typeof(DetectionEvaluator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0
            ? new BuildVersion(informational, "")
            : new BuildVersion(informational[..plus], informational[(plus + 1)..]);
    }
}
