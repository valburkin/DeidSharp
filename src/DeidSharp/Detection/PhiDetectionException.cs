namespace DeidSharp.Detection;

/// <summary>
/// A detector failed or returned something unusable. De-identification stops instead
/// of continuing with a detector missing: skipping one would silently lower recall.
/// </summary>
public sealed class PhiDetectionException : Exception
{
    public PhiDetectionException()
    {
    }

    public PhiDetectionException(string message)
        : base(message)
    {
    }

    public PhiDetectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
