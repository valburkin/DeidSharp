namespace DeidSharp.Text;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: what a found identifier turns into. A tag ("[NAME]") makes the redaction
// obvious; a surrogate ("Laura Bennett") hides any missed identifier among fakes. The
// text de-identifier applies whichever strategy it was built with.

/// <summary>Chooses the text that replaces one PHI span.</summary>
public interface IReplacementStrategy
{
    /// <param name="original">The text the span covered (never logged or returned).</param>
    string Replace(PhiSpan span, string original, TextContext context);
}
