namespace DeidSharp.Detection;

/// <summary>
/// Rejects a list in which two items share a name. Names are recorded as the source of
/// every span, so each must say exactly which detector or rule found what.
/// </summary>
internal static class UniqueNames
{
    /// <param name="kind">What is named, for the message, e.g. "Detector".</param>
    public static void Ensure(IEnumerable<string> names, string kind, string paramName)
    {
        var duplicate = names.GroupBy(name => name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"{kind} names must be unique; '{duplicate.Key}' appears twice.", paramName);
        }
    }
}
