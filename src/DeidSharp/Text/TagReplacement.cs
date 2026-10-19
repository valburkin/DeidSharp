using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace DeidSharp.Text;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: the tag strategy. Every span becomes its category's tag, so a reader sees
// exactly what was removed. SurrogateReplacement also falls back to it when no safe
// fake exists.

/// <summary>
/// Replaces each span with a category tag: "[NAME]", "[DATE]", "[MRN]". Readers see
/// exactly what was removed; nothing in the output can be mistaken for real data.
/// </summary>
public sealed class TagReplacement : IReplacementStrategy
{
    private static readonly FrozenDictionary<PhiCategory, string> Tags = new Dictionary<PhiCategory, string>
    {
        [PhiCategory.Name] = "NAME",
        [PhiCategory.StreetAddress] = "ADDRESS",
        [PhiCategory.Location] = "LOCATION",
        [PhiCategory.ZipCode] = "ZIP",
        [PhiCategory.Date] = "DATE",
        [PhiCategory.Age] = "AGE",
        [PhiCategory.Phone] = "PHONE",
        [PhiCategory.Fax] = "FAX",
        [PhiCategory.Email] = "EMAIL",
        [PhiCategory.Ssn] = "SSN",
        [PhiCategory.MedicalRecordNumber] = "MRN",
        [PhiCategory.HealthPlanNumber] = "HEALTH_PLAN_ID",
        [PhiCategory.AccountNumber] = "ACCOUNT",
        [PhiCategory.LicenseNumber] = "LICENSE",
        [PhiCategory.VehicleIdentifier] = "VEHICLE_ID",
        [PhiCategory.DeviceIdentifier] = "DEVICE_ID",
        [PhiCategory.Url] = "URL",
        [PhiCategory.IpAddress] = "IP_ADDRESS",
        [PhiCategory.Biometric] = "BIOMETRIC",
        [PhiCategory.Photo] = "PHOTO",
        [PhiCategory.OtherIdentifier] = "ID",
        [PhiCategory.Organization] = "ORGANIZATION",
    }.ToFrozenDictionary();

    private readonly CompositeFormat _format;

    /// <param name="format">How a tag is written; "{0}" is the tag name. Default "[{0}]".</param>
    public TagReplacement(string format = "[{0}]")
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        _format = CompositeFormat.Parse(format);
        if (_format.MinimumArgumentCount != 1)
        {
            throw new ArgumentException("The format must use {0} for the tag name.", nameof(format));
        }
    }

    /// <summary>The tag name for a category, e.g. "MRN".</summary>
    public static string TagName(PhiCategory category) => Tags.GetValueOrDefault(category, "PHI");

    public string Replace(PhiSpan span, string original, TextContext context)
    {
        ArgumentNullException.ThrowIfNull(span);
        return Tag(span.Category);
    }

    /// <summary>The tag for a category, written in this instance's format, e.g. "[MRN]".</summary>
    public string Tag(PhiCategory category) => string.Format(CultureInfo.InvariantCulture, _format, TagName(category));
}
