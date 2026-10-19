using DeidSharp.Detection;

namespace DeidSharp.Text;

/// <summary>
/// Who a piece of text is about. <see cref="SubjectId"/> keys per-subject choices (one
/// date shift per patient, consistent surrogates); <see cref="Known"/> lists the
/// subject's own identifiers so detectors can look for them and surrogates avoid them.
/// </summary>
public sealed record TextContext
{
    public TextContext(string subjectId = "", KnownIdentifiers? known = null)
    {
        ArgumentNullException.ThrowIfNull(subjectId);
        SubjectId = subjectId;
        Known = known ?? KnownIdentifiers.Empty;
    }

    public static TextContext None { get; } = new();

    public string SubjectId { get; }

    public KnownIdentifiers Known { get; }
}
