namespace DeidSharp.Presidio;

/// <summary>
/// Converts Presidio's offsets to .NET string indices. Presidio is Python, and Python
/// counts code points; .NET strings count UTF-16 code units. They differ after any
/// character outside the Basic Multilingual Plane (an emoji, some CJK): "😀 John" has
/// John at code point 2 but at index 3. Using Python offsets directly would redact the
/// wrong characters and leave part of the name in place.
/// </summary>
internal sealed class CodePointIndex
{
    // _utf16Offsets[cp] is the UTF-16 index of code point cp, plus one entry for the end.
    // Null when the text has no surrogate pairs: both counts are then the same.
    private readonly int[]? _utf16Offsets;
    private readonly int _length;

    public CodePointIndex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _length = text.Length;
        if (!text.Any(char.IsSurrogate))
        {
            return;
        }

        var offsets = new List<int>(text.Length + 1);
        for (var i = 0; i < text.Length; i++)
        {
            offsets.Add(i);
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
        }

        offsets.Add(text.Length);
        _utf16Offsets = [.. offsets];
    }

    /// <summary>The UTF-16 index of a code-point offset, or -1 if the offset is outside the text.</summary>
    public int ToUtf16(int codePointOffset)
    {
        if (_utf16Offsets is null)
        {
            return codePointOffset >= 0 && codePointOffset <= _length ? codePointOffset : -1;
        }

        return codePointOffset >= 0 && codePointOffset < _utf16Offsets.Length ? _utf16Offsets[codePointOffset] : -1;
    }
}
