namespace DeidSharp;

/// <summary>
/// The kinds of identifiers DeidSharp detects and removes. The first eighteen follow the
/// HIPAA Safe Harbor list (45 CFR 164.514(b)(2)(i), letters A–R); <see cref="Organization"/>
/// is not on that list but names employers and care sites, which clinical text often does.
/// </summary>
public enum PhiCategory
{
    /// <summary>(A) Names of the patient, relatives, employers or household members, and of staff.</summary>
    Name,

    /// <summary>(B) Street address, including house number and apartment.</summary>
    StreetAddress,

    /// <summary>(B) City, county, precinct or other geographic subdivision smaller than a state.</summary>
    Location,

    /// <summary>(B) ZIP code (only the first three digits may be kept, and not for sparse areas).</summary>
    ZipCode,

    /// <summary>(C) Any element of a date except the year.</summary>
    Date,

    /// <summary>(C) An age over 89.</summary>
    Age,

    /// <summary>(D) Telephone number.</summary>
    Phone,

    /// <summary>(E) Fax number.</summary>
    Fax,

    /// <summary>(F) Email address.</summary>
    Email,

    /// <summary>(G) Social Security number.</summary>
    Ssn,

    /// <summary>(H) Medical record number.</summary>
    MedicalRecordNumber,

    /// <summary>(I) Health plan beneficiary number.</summary>
    HealthPlanNumber,

    /// <summary>(J) Account number, including payment card numbers.</summary>
    AccountNumber,

    /// <summary>(K) Certificate or license number.</summary>
    LicenseNumber,

    /// <summary>(L) Vehicle identifier or serial number, including license plates.</summary>
    VehicleIdentifier,

    /// <summary>(M) Device identifier or serial number.</summary>
    DeviceIdentifier,

    /// <summary>(N) Web URL.</summary>
    Url,

    /// <summary>(O) IP address.</summary>
    IpAddress,

    /// <summary>(P) Biometric identifier, such as a finger or voice print.</summary>
    Biometric,

    /// <summary>(Q) Full-face photograph or comparable image.</summary>
    Photo,

    /// <summary>(R) Any other unique identifying number, characteristic or code.</summary>
    OtherIdentifier,

    /// <summary>A hospital, clinic, employer or other organization name.</summary>
    Organization,
}
