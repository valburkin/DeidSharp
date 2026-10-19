using System.Text;
using Hl7.Fhir.Model;

namespace DeidSharp.Fhir;

/// <summary>
/// Decides whether an attachment is inline text that can be read, and reads it. Only
/// <c>text/*</c> declared as UTF-8 or US-ASCII (or with no charset, which FHIR treats as
/// UTF-8) qualifies, and only if it decodes strictly and holds no control characters
/// other than tab, line feed, carriage return and form feed. Anything else might decode
/// into something no detector can read (UTF-16 decoded as UTF-8 is "J\0o\0h\0n"), so it
/// is treated as uninspectable and removed.
/// </summary>
internal static class InlineText
{
    private const char ByteOrderMark = '﻿';

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryDecode(Attachment attachment, out string text)
    {
        text = string.Empty;
        if (attachment.Data is not { Length: > 0 } data || attachment.ContentType is not { } contentType || !IsReadableTextType(contentType))
        {
            return false;
        }

        try
        {
            text = StrictUtf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        if (text.StartsWith(ByteOrderMark))
        {
            text = text[1..];
        }

        return !text.Any(c => char.IsControl(c) && c is not ('\t' or '\n' or '\r' or '\f'));
    }

    /// <summary>True for an attachment whose content is inline text that can be read and de-identified.</summary>
    public static bool IsReadable(Attachment attachment) => TryDecode(attachment, out _);

    public static byte[] Encode(string text) => StrictUtf8.GetBytes(text);

    private static bool IsReadableTextType(string contentType)
    {
        var parts = contentType.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0
            && parts[0].StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            && parts.Skip(1).All(IsReadableCharset);
    }

    // A parameter other than charset ("format=flowed") doesn't change how the bytes read.
    private static bool IsReadableCharset(string parameter)
    {
        var nameAndValue = parameter.Split('=', 2, StringSplitOptions.TrimEntries);
        return !nameAndValue[0].Equals("charset", StringComparison.OrdinalIgnoreCase)
            || (nameAndValue.Length == 2 && nameAndValue[1].Trim('"').ToUpperInvariant() is "UTF-8" or "UTF8" or "US-ASCII" or "ASCII");
    }
}
