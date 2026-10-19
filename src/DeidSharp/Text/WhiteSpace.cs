namespace DeidSharp.Text;

/// <summary>Splits text into words at any run of white space.</summary>
internal static class WhiteSpace
{
    // A null separator means "any white-space character" (spaces, tabs, line breaks).
    public static string[] Split(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
