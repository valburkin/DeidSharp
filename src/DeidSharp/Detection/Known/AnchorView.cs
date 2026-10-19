namespace DeidSharp.Detection.Known;

/// <summary>The view of a text that an anchor is looked for in (see <see cref="AnchorIndex"/>).</summary>
internal enum AnchorView
{
    /// <summary>Anywhere in the folded text, ignoring case.</summary>
    Folded,

    /// <summary>Anywhere in the folded text with the separators of <c>DigitGap</c> dropped, ignoring case.</summary>
    Compact,

    /// <summary>A whole run of ASCII digits: "1931" in "3/4/1931", but not in "19310".</summary>
    DigitRun,
}
