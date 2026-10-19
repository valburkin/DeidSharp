namespace DeidSharp.Verification;

/// <summary>
/// De-identified output still contains something that looks like PHI. The message lists
/// paths, positions and categories only, never the values.
/// </summary>
public sealed class ResidualPhiException : Exception
{
    public ResidualPhiException()
        : this(new LeakReport([]))
    {
    }

    public ResidualPhiException(string message)
        : base(message)
    {
        Report = new LeakReport([]);
    }

    public ResidualPhiException(string message, Exception innerException)
        : base(message, innerException)
    {
        Report = new LeakReport([]);
    }

    public ResidualPhiException(LeakReport report)
        : base(BuildMessage(report))
    {
        Report = report;
    }

    public LeakReport Report { get; }

    private static string BuildMessage(LeakReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return "De-identified output still contains possible PHI: " + string.Join("; ", report.Describe());
    }
}
