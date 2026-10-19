namespace DeidSharp.Detection;

/// <summary>
/// The text to search, plus the identifiers already known from the structured record
/// (the patient's name, birth date, MRN…), which a detector may look for directly.
/// </summary>
public sealed record DetectionRequest
{
    public DetectionRequest(string text, KnownIdentifiers? known = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Known = known ?? KnownIdentifiers.Empty;
    }

    public string Text { get; }

    public KnownIdentifiers Known { get; }
}
