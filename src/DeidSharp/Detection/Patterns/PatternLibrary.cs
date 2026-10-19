using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using DeidSharp.Dates;

namespace DeidSharp.Detection.Patterns;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each kind of shaped identifier is one PatternRule row. A new kind is a new row
// (or a caller's own list passed to PatternPhiDetector), never a new branch in the
// detector. Not a design-pattern library, despite the name: "pattern" here means a
// regular expression.

/// <summary>
/// The default pattern rules, one per kind of shaped identifier.
///
/// Trade-offs, chosen for recall on clinical text without wrecking clinical content:
/// numeric dates need a year ("3/5/2024", not "3/5", which is how pain scores and blood
/// pressures are written); a bare year is kept (Safe Harbor allows it); five-digit ZIP
/// codes are only taken next to a state or a label, since lab values are five-digit
/// numbers too; names are only found after a title or label (NER finds the rest).
/// </summary>
public static partial class PatternLibrary
{
    private const RegexOptions Ci = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private const RegexOptions Cs = RegexOptions.CultureInvariant;
    private const int Timeout = 1000;

    private const string L = @"(?<![\p{L}\p{N}])";
    private const string R = @"(?![\p{L}\p{N}])";

    // Month names; "March" and "May" only when capitalized, since "march" and "may"
    // are ordinary words.
    private const string Month =
        "(?:jan(?:uary)?|feb(?:ruary)?|(?-i:M)ar(?:ch)?|apr(?:il)?|(?-i:M)ay|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)";

    private const string States =
        "AL|AK|AZ|AR|CA|CO|CT|DE|DC|FL|GA|HI|ID|IL|IN|IA|KS|KY|LA|ME|MD|MA|MI|MN|MS|MO|MT|NE|NV|NH|NJ|NM|NY|NC|ND|OH|OK|OR|PA|RI|SC|SD|TN|TX|UT|VT|VA|WA|WV|WI|WY|PR|VI|GU|AS|MP";

    // Two-letter states that are also common words or abbreviations in notes ("OK",
    // "IN", "ME", "PA" for physician assistant), left out where the state is the only clue.
    private const string UnambiguousStates =
        "AL|AK|AZ|AR|CA|CO|CT|DC|FL|GA|IL|IA|KS|KY|LA|MD|MA|MI|MN|MS|MO|MT|NE|NV|NH|NJ|NM|NY|NC|ND|OH|RI|SC|SD|TN|TX|UT|VT|VA|WA|WV|WI|WY|PR";

    private const string StreetSuffix =
        "Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Lane|Ln|Drive|Dr|Court|Ct|Place|Pl|Way|Terrace|Ter|Circle|Cir|" +
        "Parkway|Pkwy|Highway|Hwy|Square|Sq|Trail|Trl|Alley|Aly|Annex|Bypass|Center|Ctr|Crossing|Xing|Crescent|Cres|" +
        "Estates?|Expressway|Expy|Extension|Ext|Freeway|Fwy|Gardens?|Gateway|Glens?|Grove|Harbou?r|Heights|Hts|Hill|" +
        "Hollow|Junction|Jct|Landing|Light|Lodge|Loop|Manors?|Meadows?|Mills?|Mission|Mount|Mt|Mountain|Orchard|Park|" +
        "Pass|Path|Pike|Plaza|Plz|Points?|Port|Rapids?|Ramp|Ridge|Row|Run|Station|Stream|Summit|Track|Turnpike|Tpke|" +
        "Valley|View|Village|Vista|Walk|Wells?|Haven|Shoals?|Springs?|Brook|Creek|Falls|Ferry|Fork|Isle|Knolls?|Lakes?|" +
        "Locks|Motorway|Pines|Plains|Prairie|River|Route|Shore|Skyway|Trace|Tunnel|Viaduct|Ville|Mews|Close|Commons?|" +
        "Corners?|Cove|Curve|Dale|Fields?|Fort|Greens|Island|Ranch|Ridges|Squares|Streets|Valleys|Views|Ways";

    // A label's optional "#", "no.", "number" and ":", then an optional "is"/"was".
    private const string LabelTail = @"\.?(?:\s*(?:#|no\.?|num(?:ber)?\.?))?\s*[:#=]?\s*(?:(?:is|was)\s+)?";

    // An identifier value: 4–32 letters, digits and inner dashes. Validators require a digit.
    private const string IdValue = @"(?<v>[A-Za-z0-9](?:[A-Za-z0-9\-]{2,30})[A-Za-z0-9])" + R;

    private const string NanpPhone = @"(?:\+?1[\s.\-]?)?(?:\(\d{3}\)\s?|\d{3}[\s.\-])\d{3}[\s.\-]\d{4}";

    // A capitalized word in any cased script ("Smith", "Álvarez", "Ñúñez"), and an
    // all-capitals one ("SMITH") for where a title or label makes it a name.
    private const string NameWord = @"\p{Lu}\p{Ll}[\p{L}\p{M}'\-]*";
    private const string CapsWord = @"\p{Lu}{2,}(?:['\-]\p{Lu}+)*";

    /// <summary>The rules <see cref="PatternPhiDetector"/> runs by default.</summary>
    public static IReadOnlyList<PatternRule> Default { get; } =
    [
        // Contact details and network identifiers.
        new("email", PhiCategory.Email, Email(), 0.95),
        new("url", PhiCategory.Url, Url(), 0.9),
        new("ipv4", PhiCategory.IpAddress, Ipv4Address(), 0.8),
        new("ipv6", PhiCategory.IpAddress, Ipv6Address(), 0.8, Validator: IsIpv6Address),
        new("mac-address", PhiCategory.DeviceIdentifier, MacAddress(), 0.85),
        new("fax", PhiCategory.Fax, Fax(), 0.95, "v"),
        new("phone", PhiCategory.Phone, Phone(), 0.85),
        new("phone-international", PhiCategory.Phone, InternationalPhone(), 0.8),
        new("phone-local", PhiCategory.Phone, LocalPhone(), 0.5),

        // Government and account numbers.
        new("ssn", PhiCategory.Ssn, Ssn(), 0.85),
        new("ssn-spaced", PhiCategory.Ssn, SsnSpaced(), 0.6),
        new("ssn-labeled", PhiCategory.Ssn, SsnLabeled(), 0.95, "v"),
        new("payment-card", PhiCategory.AccountNumber, PaymentCard(), 0.9, Validator: m => Checksums.IsValidLuhn(m.Value)),
        new("vin", PhiCategory.VehicleIdentifier, Vin(), 0.9, Validator: m => Checksums.IsValidVin(m.Value)),

        // Labeled identifiers: the label decides the category.
        new("mrn-labeled", PhiCategory.MedicalRecordNumber, MrnLabeled(), 0.95, "v", HasDigit),
        new("health-plan-labeled", PhiCategory.HealthPlanNumber, HealthPlanLabeled(), 0.9, "v", HasDigit),
        new("account-labeled", PhiCategory.AccountNumber, AccountLabeled(), 0.9, "v", HasDigit),
        new("license-labeled", PhiCategory.LicenseNumber, LicenseLabeled(), 0.9, "v", HasDigit),
        new("vehicle-labeled", PhiCategory.VehicleIdentifier, VehicleLabeled(), 0.95, "v", HasDigit),
        new("device-labeled", PhiCategory.DeviceIdentifier, DeviceLabeled(), 0.9, "v", HasDigit),
        new("id-labeled", PhiCategory.OtherIdentifier, OtherIdLabeled(), 0.9, "v", HasDigit),
        new("long-number", PhiCategory.OtherIdentifier, LongNumber(), 0.5),

        // Dates (any element finer than the year).
        new("date-iso", PhiCategory.Date, IsoDate(), 0.9, Validator: IsValidIsoDate),
        new("date-compact", PhiCategory.Date, CompactDate(), 0.6),
        new("date-numeric", PhiCategory.Date, NumericDate(), 0.8, Validator: IsValidNumericDate),
        new("date-month-day", PhiCategory.Date, MonthDayDate(), 0.85, Validator: HasValidDay),
        new("date-day-month", PhiCategory.Date, DayMonthDate(), 0.85, Validator: HasValidDay),
        new("date-year-month-day", PhiCategory.Date, YearMonthDayDate(), 0.85, Validator: HasValidDay),
        new("date-month-year", PhiCategory.Date, MonthYearDate(), 0.85),
        new("date-numeric-month-year", PhiCategory.Date, NumericMonthYearDate(), 0.6),

        // Ages over 89 (the number only, so "92-year-old" reads "[AGE]-year-old").
        new("age-over-89", PhiCategory.Age, AgeOver89(), 0.9, "v"),
        new("age-over-89-labeled", PhiCategory.Age, AgeOver89Labeled(), 0.9, "v"),

        // Geography smaller than a state.
        new("street-address", PhiCategory.StreetAddress, StreetAddress(), 0.85),
        new("po-box", PhiCategory.StreetAddress, PoBox(), 0.9),
        new("zip-after-state", PhiCategory.ZipCode, ZipAfterState(), 0.9, "v"),
        new("zip-labeled", PhiCategory.ZipCode, ZipLabeled(), 0.95, "v"),
        new("city-before-state", PhiCategory.Location, CityBeforeState(), 0.7, "v"),

        // Names that a title or a label gives away.
        new("name-after-title", PhiCategory.Name, NameAfterTitle(), 0.7, "v"),
        new("name-labeled", PhiCategory.Name, NameLabeled(), 0.8, "v"),
        new("name-after-relation", PhiCategory.Name, NameAfterRelation(), 0.6, "v"),
    ];

    private static bool HasDigit(Match m) => m.Groups["v"].Value.Any(char.IsAsciiDigit);

    private static bool IsIpv6Address(Match m) =>
        (m.Value.Contains("::", StringComparison.Ordinal) || m.Value.Count(c => c == ':') == 7)
        && IPAddress.TryParse(m.Value, out var ip)
        && ip.AddressFamily == AddressFamily.InterNetworkV6;

    private static bool IsValidIsoDate(Match m) =>
        IsMonth(m.Groups["m"].Value) && IsDay(m.Groups["d"].Value);

    private static bool IsValidNumericDate(Match m)
    {
        var a = Int(m.Groups["a"].Value);
        var b = Int(m.Groups["b"].Value);
        return a >= 1 && b >= 1 && ((a <= 12 && b <= 31) || (b <= 12 && a <= 31));
    }

    private static bool HasValidDay(Match m) => IsDay(m.Groups["d"].Value) && TextDateParser.MonthFromName(m.Groups["mon"].Value) != 0;

    private static bool IsMonth(string value) => Int(value) is >= 1 and <= 12;

    private static bool IsDay(string value) => Int(value) is >= 1 and <= 31;

    private static int Int(string digits) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;

    [GeneratedRegex(@"(?<![\w.%+\-])[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}(?![\w\-])", Cs, Timeout)]
    private static partial Regex Email();

    [GeneratedRegex("""(?<![\w@])(?:https?://|ftp://|www\.)[^\s<>"'\[\]]*[^\s<>"'\[\].,;:!?)]""", Ci, Timeout)]
    private static partial Regex Url();

    [GeneratedRegex(@"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?!\d|\.\d)", Cs, Timeout)]
    private static partial Regex Ipv4Address();

    [GeneratedRegex("(?<![0-9A-Fa-f:])(?:[0-9A-Fa-f]{0,4}:){2,7}[0-9A-Fa-f]{0,4}(?![0-9A-Fa-f:])", Cs, Timeout)]
    private static partial Regex Ipv6Address();

    [GeneratedRegex(@"(?<![0-9A-Fa-f:\-])(?:[0-9A-Fa-f]{2}([:\-]))(?:[0-9A-Fa-f]{2}\1){4}[0-9A-Fa-f]{2}(?![0-9A-Fa-f:\-])", Cs, Timeout)]
    private static partial Regex MacAddress();

    [GeneratedRegex(L + "(?:fax|facsimile)" + LabelTail + "(?<v>" + NanpPhone + @")(?!\d)", Ci, Timeout)]
    private static partial Regex Fax();

    [GeneratedRegex(@"(?<![\d+])" + NanpPhone + @"(?!\d)", Cs, Timeout)]
    private static partial Regex Phone();

    [GeneratedRegex(@"(?<![\w+])\+(?:\d[\s.\-]?){7,14}\d(?!\d)", Cs, Timeout)]
    private static partial Regex InternationalPhone();

    // Not a range followed by a unit: "250-1000 mg" is a dose.
    [GeneratedRegex(@"(?<![\d\-.()])\d{3}-\d{4}(?![\d\-])(?!\s*(?i:mg|mcg|µg|ug|g|kg|ml|l|units?|iu|meq|mmol|mol|%|x\b|times))", Cs, Timeout)]
    private static partial Regex LocalPhone();

    [GeneratedRegex(@"(?<![\d\-])\d{3}-\d{2}-\d{4}(?![\d\-])", Cs, Timeout)]
    private static partial Regex Ssn();

    [GeneratedRegex(@"(?<!\d\s?)\d{3} \d{2} \d{4}(?!\s?\d)", Cs, Timeout)]
    private static partial Regex SsnSpaced();

    [GeneratedRegex(L + @"(?:ssn|ss\#|social\s+security(?:\s+(?:number|no\.?|\#))?)" + LabelTail + @"(?<v>\d{3}[\-\s]?\d{2}[\-\s]?\d{4})(?!\d)", Ci, Timeout)]
    private static partial Regex SsnLabeled();

    [GeneratedRegex(@"(?<![\d\-])\d(?:[ \-]?\d){12,18}(?![\d\-]|[ \-]\d)", Cs, Timeout)]
    private static partial Regex PaymentCard();

    [GeneratedRegex("(?<![A-Za-z0-9])[A-HJ-NPR-Z0-9]{17}(?![A-Za-z0-9])", Cs, Timeout)]
    private static partial Regex Vin();

    [GeneratedRegex(L + @"(?:mrn|medical\s+record(?:\s+(?:number|no\.?|\#))?|med(?:ical)?\s+rec(?:ord)?|chart\s+(?:number|no\.?|\#)|hospital\s+(?:number|no\.?|\#)|unit\s+(?:number|no\.?))" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex MrnLabeled();

    [GeneratedRegex(L + @"(?:member\s+id|subscriber\s+id|policy|insurance\s+id|health\s+plan(?:\s+id)?|medicaid(?:\s+id)?|medicare(?:\s+id)?|beneficiary\s+id|group\s+(?:number|no\.?|\#)|plan\s+id)" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex HealthPlanLabeled();

    [GeneratedRegex(L + @"(?:acct|account|billing\s+(?:id|number|no\.?)|invoice|encounter\s+(?:id|number|no\.?)|visit\s+(?:id|number|no\.?)|fin|csn)" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex AccountLabeled();

    [GeneratedRegex(L + @"(?:licen[cs]e|lic|dea|npi|certificate|cert|driver'?s\s+licen[cs]e)" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex LicenseLabeled();

    [GeneratedRegex(L + @"(?:vin|licen[cs]e\s+plate|plate|vehicle\s+id)" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex VehicleLabeled();

    [GeneratedRegex(L + @"(?:serial|s/n|sn|device\s+id|udi|lot\s+(?:number|no\.?|\#)|imei|model\s+(?:number|no\.?)|implant\s+id)" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex DeviceLabeled();

    [GeneratedRegex(L + @"(?:patient\s+id|pt\s+id|person\s+id|case\s+(?:id|number|no\.?|\#)|passport|employee\s+id|badge|national\s+id|id\s+(?:number|no\.?|\#)|identifier)" + LabelTail + IdValue, Ci, Timeout)]
    private static partial Regex OtherIdLabeled();

    // Not after a code-system name: "SNOMED 433144002" is a concept code, not a person's number.
    [GeneratedRegex(@"(?<![\d.,\-/])(?<!(?i:snomed(?:\s*ct)?|loinc|rxnorm|rxcui|cpt|hcpcs|ndc|icd-?(?:9|10)(?:-?cm)?|cvx|umls|cui)(?:\s*code)?[\s:#]*)\d{8,}(?![\d,\-/]|\.\d)", Cs, Timeout)]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"(?<![\d.])(?<y>\d{4})(?<s>[\-/.])(?<m>\d{1,2})\k<s>(?<d>\d{1,2})(?!\d|\.\d)", Cs, Timeout)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"(?<![\d.])(?:19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])(?!\d|\.\d)", Cs, Timeout)]
    private static partial Regex CompactDate();

    [GeneratedRegex(@"(?<![\d./\-])(?<a>\d{1,2})(?<s>[\-/.])(?<b>\d{1,2})\k<s>(?<y>\d{4}|\d{2})(?![\d/\-]|\.\d)", Cs, Timeout)]
    private static partial Regex NumericDate();

    // "March 5, 2024", "Mar-05-2024", "MAR 5".
    [GeneratedRegex(L + "(?<mon>" + Month + @")\.?(?:\s+|[\-/.]\s*)(?<d>\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(?<y>\d{4})|[\-/.]\s*(?<y>\d{4}|\d{2}))?" + R, Ci, Timeout)]
    private static partial Regex MonthDayDate();

    // "5 March 2024", "5th of March", "05-Mar-2024", "05MAR1950" (lab and EHR style).
    [GeneratedRegex(L + @"(?<d>\d{1,2})(?:st|nd|rd|th)?(?:\s+(?:of\s+)?|[\-/.]\s*|(?=[A-Za-z]))(?<mon>" + Month + @")\.?(?:,?\s+(?<y>\d{4})|[\-/.]?\s*(?<y>\d{4})|[\-/.](?<y>\d{2}))?" + R, Ci, Timeout)]
    private static partial Regex DayMonthDate();

    // "2024-Mar-05", "2024 March 5".
    [GeneratedRegex(L + @"(?<y>\d{4})[\-/.\s](?<mon>" + Month + @")\.?[\-/.\s](?<d>\d{1,2})" + R, Ci, Timeout)]
    private static partial Regex YearMonthDayDate();

    [GeneratedRegex(L + Month + @"\.?,?\s+(?:of\s+)?\d{4}" + R, Ci, Timeout)]
    private static partial Regex MonthYearDate();

    [GeneratedRegex(@"(?<![\d/.])(?:0?[1-9]|1[0-2])/(?:19|20)\d{2}(?![\d/])", Cs, Timeout)]
    private static partial Regex NumericMonthYearDate();

    [GeneratedRegex(L + @"(?<v>9\d|1[0-4]\d)(?:\s*-\s*|\s+)?(?:years?|yrs?|y/o|yo|y\.o\.?)(?:[\s\-]*old)?(?![\p{L}\p{N}])", Ci, Timeout)]
    private static partial Regex AgeOver89();

    [GeneratedRegex(L + @"(?:aged?|age\s*[:=])\s*(?<v>9\d|1[0-4]\d)(?![\p{L}\p{N}]|\.\d)", Ci, Timeout)]
    private static partial Regex AgeOver89Labeled();

    [GeneratedRegex(L + @"\d{1,6}[A-Za-z]?\s+(?:[NSEW]\.?\s+|(?:North|South|East|West)\s+)?(?:[A-Z][A-Za-z0-9'\-]*\.?\s+){1,4}(?:" + StreetSuffix + @")(?:\.?,?\s*(?:Apt|Apartment|Suite|Ste|Unit|Rm|Room|Bldg|Building|Fl|Floor|\#)\.?\s*\#?[A-Za-z0-9\-]+)?" + R, Cs, Timeout)]
    private static partial Regex StreetAddress();

    [GeneratedRegex(L + @"P\.?\s?O\.?\s*Box\s+\d+" + R, Ci, Timeout)]
    private static partial Regex PoBox();

    [GeneratedRegex(@"(?<![\p{L}])(?:" + States + @"),?\s+(?<v>\d{5}(?:-\d{4})?)(?!\d)", Cs, Timeout)]
    private static partial Regex ZipAfterState();

    [GeneratedRegex(L + @"(?:zip(?:\s*code)?|postal\s*code|post\s*code)\s*[:#]?\s*(?<v>\d{5}(?:-\d{4})?)(?!\d)", Ci, Timeout)]
    private static partial Regex ZipLabeled();

    [GeneratedRegex(@"(?<![\p{L}])(?<v>" + NameWord + @"(?:[ \-]" + NameWord + @"){0,2}),\s*(?:" + UnambiguousStates + @")(?![\p{L}])", Cs, Timeout)]
    private static partial Regex CityBeforeState();

    [GeneratedRegex(@"(?<![\p{L}])(?:Mr|Mrs|Ms|Mx|Miss|Dr|Prof|MR|MRS|MS|DR)\.?\s+(?<v>(?:" + NameWord + "|" + CapsWord + @")(?:\s+\p{Lu}\.)?(?:\s+(?:" + NameWord + "|" + CapsWord + "))?)", Cs, Timeout)]
    private static partial Regex NameAfterTitle();

    [GeneratedRegex(@"(?<![\p{L}])(?i:name|patient(?:\s+name)?|pt|attending|physician|provider|nurse|pcp|signed\s+by|dictated\s+by|seen\s+by|referred\s+by|electronically\s+signed\s+by|cc)\s*:\s*(?<v>" + NameWord + @"(?:\s+\p{Lu}\.)?(?:\s+" + NameWord + "){0,2}|" + CapsWord + @"(?:\s+" + CapsWord + "){1,2})", Cs, Timeout)]
    private static partial Regex NameLabeled();

    [GeneratedRegex(@"(?<![\p{L}])(?i:son|daughter|wife|husband|mother|father|brother|sister|spouse|partner|guardian|caregiver|grandson|granddaughter|niece|nephew|aunt|uncle|friend|neighbou?r|roommate)(?:'s)?,?\s+(?<v>" + NameWord + @"(?:\s+" + NameWord + ")?)", Cs, Timeout)]
    private static partial Regex NameAfterRelation();
}
