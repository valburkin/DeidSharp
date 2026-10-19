using System.Collections.Frozen;

namespace DeidSharp.Presidio;

/// <summary>
/// Presidio entity types → DeidSharp categories. Covers Presidio's built-in recognizers
/// and the entity names the clinical sidecar maps its i2b2 labels to. An entity type
/// not listed here becomes <see cref="PhiCategory.OtherIdentifier"/> (fail-safe: an
/// unknown finding is still redacted).
/// </summary>
public static class PresidioEntityMap
{
    public static IReadOnlyDictionary<string, PhiCategory> Default { get; } = new Dictionary<string, PhiCategory>(StringComparer.OrdinalIgnoreCase)
    {
        ["PERSON"] = PhiCategory.Name,
        ["LOCATION"] = PhiCategory.Location,
        ["ORGANIZATION"] = PhiCategory.Organization,
        ["DATE_TIME"] = PhiCategory.Date,
        ["AGE"] = PhiCategory.Age,
        ["PHONE_NUMBER"] = PhiCategory.Phone,
        ["EMAIL_ADDRESS"] = PhiCategory.Email,
        ["URL"] = PhiCategory.Url,
        ["IP_ADDRESS"] = PhiCategory.IpAddress,
        ["MAC_ADDRESS"] = PhiCategory.DeviceIdentifier,
        ["US_SSN"] = PhiCategory.Ssn,
        ["US_ITIN"] = PhiCategory.Ssn,
        ["MEDICAL_RECORD_NUMBER"] = PhiCategory.MedicalRecordNumber,
        ["MEDICAL_LICENSE"] = PhiCategory.LicenseNumber,
        ["US_DRIVER_LICENSE"] = PhiCategory.LicenseNumber,
        ["US_PASSPORT"] = PhiCategory.OtherIdentifier,
        ["UK_NHS"] = PhiCategory.HealthPlanNumber,
        ["CREDIT_CARD"] = PhiCategory.AccountNumber,
        ["IBAN_CODE"] = PhiCategory.AccountNumber,
        ["US_BANK_NUMBER"] = PhiCategory.AccountNumber,
        ["ID"] = PhiCategory.OtherIdentifier,
        ["STREET_ADDRESS"] = PhiCategory.StreetAddress,
        ["ZIP_CODE"] = PhiCategory.ZipCode,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static PhiCategory Resolve(string entityType, IDictionary<string, PhiCategory>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (overrides is not null && overrides.TryGetValue(entityType, out var custom))
        {
            return custom;
        }

        return Default.GetValueOrDefault(entityType, PhiCategory.OtherIdentifier);
    }
}
